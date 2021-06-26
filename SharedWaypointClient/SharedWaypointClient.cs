using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MenuAPI;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace SharedWaypointClient {

    public static class UI {
        public const string MainMenuTitle = "Shared Waypoints";
        public const string WaypointsSubmenuTitle = "Shared Waypoints";
        public const string MainMenuSubtitleDefault = "Status: Not following or sharing";
        public const string MainMenuSubtitleFollowing = "Status: Following ";
        public const string MainMenuSubtitlePublishing = "Status: Sharing waypoints. ";
        public const string WaypointsSubmenuSubtitleDefault = "Nobody is sharing waypoints right now";
        public const string WaypointsSubmenuSubtitleAvailable = "Choose a player to follow";
        public const string ToggleSharingText = "Share my waypoint";
        public const string ToggleSharingDescription = "Share your current waypoint with other players.  Will update whenever you change your waypoint, until disabled";
        public const string WaypointsSubmenuItemText = "Follow another player's shared waypoint";
        public const string WaypointsSubmenuItemDescriptionEnabled = "Select this option to see a list of players who are currently sharing their waypoint.  Choose a player to follow their waypoint.";
        public const string WaypointsSubmenuItemDescriptionPublishing = "You must stop sharing your waypoint before you can follow someone else's shared waypoint.";
        public const string WaypointsSubmenuItemDescriptionSubscribed = "You are already following someone's shared waypoint.  You must unfollow them before you can follow someone else's shared waypoint.";
        public const string WaypointPublisherItemDescription = "Select to follow waypoints shared by ";
        public const string UnfollowText = "Unfollow"; 
        public const string UnfollowDescriptionDefault = "Select this to stop following a shared waypoint";
        public const string UnfollowDescriptionFollowing = "Select to stop following waypoint updates from ";
        public const string ToggleAlignmentText = "Left align menu";
        public const string ToggleAlignmentDescription = "Move this menu to the left side of the screen if enabled";
        public const string FollowingConfirmation = "You are now following waypoint updates from ";   
    }
    public class SharedWaypointClient : BaseScript {
        private int blip;
        private Vector3 coords;
        private bool publishing = false;
        private bool gotPublishers = false;
        private Menu pubsMenu;
        private Menu menu;
        private MenuItem unsub;
        private MenuItem pubsMenuItem;
        private MenuCheckboxItem box;
        private MenuCheckboxItem togglePub;

        public SharedWaypointClient() {
            EventHandlers["onClientResourceStart"] += new Action<string>(OnClientResourceStart);
            EventHandlers["onClientResourceStop"] += new Action<string>(OnClientResourceStop);
            EventHandlers["SharedWaypoint:SetWaypoint"] += new Action<Vector3>(SetWaypoint);
            EventHandlers["SharedWaypoint:ClearWaypoint"] += new Action(ClearWaypoint);
            EventHandlers["SharedWaypoint:ReceivePublisher"] += new Action<int, string>(ReceivePublisher);
            EventHandlers["SharedWaypoint:RemovePublisher"] += new Action<int>(RemovePublisher);
            EventHandlers["SharedWaypoint:ForceUnfollow"] += new Action(Unfollow);
            EventHandlers["SharedWaypoint:Trace"] += new Action<string>(WriteDebug);
        }

        private void WriteDebug(string message) {
            Debug.WriteLine(message);
        }

        private void OnClientResourceStart(string resourceName) {
            if (GetCurrentResourceName() != resourceName) return;
            MenuInitialize();
        }

        private void OnClientResourceStop(string resourceName) {
            publishing = false;
        }
        private void SetWaypoint(Vector3 coords) {
            if (DoesBlipExist(blip)) {
                SetBlipCoords(blip, coords.X, coords.Y, coords.Z);
            }
            else {
                blip = AddBlipForCoord(coords.X, coords.Y, coords.Z);
            }
            SetBlipColour(blip, 33);
            SetBlipRouteColour(blip, 33);
            SetBlipRoute(blip, true);
            WriteDebug("SetWaypoint triggered");
        }

        private void ClearWaypoint() {
            if (DoesBlipExist(blip)) {
                SetBlipRoute(blip, false);
                RemoveBlip(ref blip);
                WriteDebug("ClearWaypoint triggered");
            }
        }

        private async Task WaypointPublisher() {
            void PollWaypoint() {
                int blipEnum = GetWaypointBlipEnumId();
                int blipHandle = GetFirstBlipInfoId(blipEnum);
                Vector3 blipCoords = GetBlipCoords(blipHandle);
                if (blipCoords != coords) {
                    TriggerServerEvent("SharedWaypoint:Publish", blipCoords);
                    coords = blipCoords;
                }
            }
            WriteDebug("WaypointPublisher started");
            while (publishing) {
                PollWaypoint();
                await Delay(500); 
            }
            WriteDebug("WaypointPublisher concluded");
        }

        private void ReceivePublisher(int publisherId, string name) {
            WriteDebug("ReceivePublisher triggered");
            MenuItem newitem = new MenuItem(name, UI.WaypointPublisherItemDescription + name) { ItemData = publisherId };
            pubsMenu.AddMenuItem(newitem);
            if (pubsMenu.MenuSubtitle != UI.WaypointsSubmenuSubtitleAvailable) {
                pubsMenu.MenuSubtitle = UI.WaypointsSubmenuSubtitleAvailable;
            }
        }

        private void RemovePublisher(int publisherId) {
            WriteDebug("RemovePublisher triggered");
            foreach (MenuItem item in pubsMenu.GetMenuItems()) {
                if (item.ItemData == publisherId) {
                    pubsMenu.RemoveMenuItem(item);
                    break;
                }
            }
            if (pubsMenu.GetMenuItems().Count == 0) {
                pubsMenu.MenuSubtitle = UI.WaypointsSubmenuSubtitleDefault;
            }
        }

        private void Unfollow() {
            ClearWaypoint();
            unsub.Enabled = false;
            unsub.Description = UI.UnfollowDescriptionDefault;
            menu.MenuSubtitle = UI.MainMenuSubtitleDefault;
            pubsMenuItem.Enabled = true;
            pubsMenuItem.Description = UI.WaypointsSubmenuItemDescriptionEnabled;
        }

        private void Follow(MenuItem item) {
            TriggerServerEvent("SharedWaypoint:Subscribe", item.ItemData);
            item.Description = UI.FollowingConfirmation + item.Text;
            unsub.Enabled = true;
            unsub.Description = UI.UnfollowDescriptionFollowing + item.Text;
            menu.MenuSubtitle = UI.MainMenuSubtitleFollowing + item.Text;
            pubsMenuItem.Enabled = false;
            pubsMenuItem.Description = UI.WaypointsSubmenuItemDescriptionSubscribed;
        }

        private void StartPublishing() {
            int blipEnum = GetWaypointBlipEnumId();
            int blipHandle = GetFirstBlipInfoId(blipEnum);
            Vector3 blipCoords = GetBlipCoords(blipHandle);
            coords = blipCoords;
            publishing = true;
            TriggerServerEvent("SharedWaypoint:RegisterPublisher", blipCoords);
            _ = WaypointPublisher();
            //pubsMenuItem.Enabled = false;
            pubsMenuItem.Description = UI.WaypointsSubmenuItemDescriptionPublishing;
            menu.MenuSubtitle = UI.MainMenuSubtitlePublishing;
        }

        private void StopPublishing() {
            TriggerServerEvent("SharedWaypoint:UnregisterPublisher");
            publishing = false;
            //pubsMenuItem.Enabled = true;
            pubsMenuItem.Description = UI.WaypointsSubmenuItemDescriptionEnabled;
            menu.MenuSubtitle = UI.MainMenuSubtitleDefault;
        }

        private void MenuInitialize() {
            //general config
            MenuController.MenuAlignment = MenuController.MenuAlignmentOption.Right;
            MenuController.MenuToggleKey = CitizenFX.Core.Control.SelectCharacterMichael; //F5 default

            //main menu & items
            menu = new Menu(UI.MainMenuTitle, UI.MainMenuSubtitleDefault);
            togglePub = new MenuCheckboxItem(UI.ToggleSharingText, UI.ToggleSharingDescription, false);
            pubsMenuItem = new MenuItem(UI.WaypointsSubmenuItemText, UI.WaypointsSubmenuItemDescriptionEnabled);
            unsub = new MenuItem(UI.UnfollowText, UI.UnfollowDescriptionDefault) { Enabled = false };
            box = new MenuCheckboxItem(UI.ToggleAlignmentText, UI.ToggleAlignmentDescription, menu.LeftAligned);
            MenuController.AddMenu(menu);
            menu.AddMenuItem(togglePub);
            menu.AddMenuItem(pubsMenuItem);
            menu.AddMenuItem(unsub);
            menu.AddMenuItem(box);

            //submenu for list of publishers
            pubsMenu = new Menu(UI.WaypointsSubmenuTitle, UI.WaypointsSubmenuSubtitleDefault);
            MenuController.AddSubmenu(menu, pubsMenu);
            MenuController.BindMenuItem(menu, pubsMenu, pubsMenuItem);

            //event handlers
            menu.OnCheckboxChange += MainMenu_OnCheckboxChange;
            menu.OnItemSelect += Menu_OnItemSelect;
            pubsMenu.OnItemSelect += Menu_OnItemSelect;
            pubsMenu.OnMenuOpen += PubsMenu_OnMenuOpen;
        }

        private void PubsMenu_OnMenuOpen(Menu menu) {
            if (!gotPublishers) {
                TriggerServerEvent("SharedWaypoint:GetActivePublishers");
                gotPublishers = true;
            }
        }
        private void Menu_OnItemSelect(Menu menu, MenuItem item, int index) {
            WriteDebug($"OnItemSelect triggered for _item.Text: {item.Text}, ItemData: {item.ItemData} ");
            if (item == unsub) {
                TriggerServerEvent("SharedWaypoint:Unsubscribe");
                ClearWaypoint();
                Unfollow();
            }
            if (menu == pubsMenu && item.ItemData != null) {
                WriteDebug($"Triggering Subscribe event with ItemData:{item.ItemData}");
                Follow(item);
            }
        }

        private void MainMenu_OnCheckboxChange(Menu menu, MenuItem item, int index, bool ischecked) {
            Debug.WriteLine($"OnCheckboxChange: [{menu}, {item}, {index}, {ischecked}]");
            if (item == box) {
                if (ischecked) {
                    MenuController.MenuAlignment = MenuController.MenuAlignmentOption.Left;
                }
                else {
                    MenuController.MenuAlignment = MenuController.MenuAlignmentOption.Right;
                }
            }
            else if (item == togglePub) {
                if (!publishing) {
                    StartPublishing();
                }
                else {
                    StopPublishing();
                }
            }
        }
    }
}