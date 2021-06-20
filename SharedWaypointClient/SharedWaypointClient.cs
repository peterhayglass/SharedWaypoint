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
    public class SharedWaypointClient : BaseScript {
        private int blip;
        private Vector3 coords;
        private bool publishing;
        private Menu pubsMenu;
        private Menu menu;
        private MenuItem unsub;
        private MenuItem pubsMenuItem;

        public SharedWaypointClient() {
            EventHandlers["onClientResourceStart"] += new Action<string>(OnClientResourceStart);
            EventHandlers["onClientResourceStop"] += new Action<string>(OnClientResourceStop);
            EventHandlers["SharedWaypoint:SetWaypoint"] += new Action<Vector3>(SetWaypoint);
            EventHandlers["SharedWaypoint:ClearWaypoint"] += new Action(ClearWaypoint);
            EventHandlers["SharedWaypoint:ReceivePublisher"] += new Action<int, string>(ReceivePublisher);
            EventHandlers["SharedWaypoint:ForceUnfollow"] += new Action(ForceUnfollow);
            EventHandlers["SharedWaypoint:Trace"] += new Action<string>(WriteDebug);
        }

        private void OnClientResourceStart(string resourceName) {
            if (GetCurrentResourceName() != resourceName) return;

            RegisterCommand("wplist", new Action<int, List<object>, string>((source, args, raw) => {
                TriggerServerEvent("SharedWaypoint:GetActivePublishers");
            }), false);

            RegisterCommand("wpshow", new Action<int, List<object>, string>((source, args, raw) => {
                var blipenum = GetWaypointBlipEnumId();
                var bliphandle = GetFirstBlipInfoId(blipenum);
                var blipcoords = GetBlipCoords(bliphandle);
                WriteDebug($"GetWaypointBlipEnumId : {blipenum}");
                WriteDebug($"blip handle (GetFirstBlipInfoId) : {bliphandle} ");
                WriteDebug($"blip coords (GetBlipCoords) : {blipcoords} ");
                //WriteDebug($"Raw command: {raw}");
            }), false);

            RegisterCommand("wpreg", new Action<int, List<object>, string>((source, args, raw) => {
                var blipenum = GetWaypointBlipEnumId();
                var bliphandle = GetFirstBlipInfoId(blipenum);
                var blipcoords = GetBlipCoords(bliphandle);
                coords = blipcoords;
                publishing = true;
                _ = WaypointPublisher();
                TriggerServerEvent("SharedWaypoint:RegisterPublisher", blipcoords);
            }), false);

            RegisterCommand("wptestpub", new Action<int, List<object>, string>((source, args, raw) => {
                Vector3 coords = new Vector3(float.Parse((string)args[0]), float.Parse((string)args[1]), float.Parse((string)args[2]));
                WriteDebug($"wptestpub is triggering SharedWaypoint:Publish with coords {coords}");
                TriggerServerEvent("SharedWaypoint:Publish", coords);
            }), false);

            RegisterCommand("wpsub", new Action<int, List<object>, string>((source, args, raw) => {
                TriggerServerEvent("SharedWaypoint:Subscribe", int.Parse((string)args[0]));
            }), false);

            RegisterCommand("wpunsub", new Action<int, List<object>, string>((source, args, raw) => {
                TriggerServerEvent("SharedWaypoint:Unsubscribe");
            }), false);

            RegisterCommand("wpunreg", new Action<int, List<object>, string>((source, args, raw) => {
                TriggerServerEvent("SharedWaypoint:UnregisterPublisher");
                publishing = false;
            }), false);

            MenuController.MenuAlignment = MenuController.MenuAlignmentOption.Right;
            menu = new Menu("Shared Waypoints", "Status: Not following anyone");
            MenuController.AddMenu(menu);
            MenuController.MenuToggleKey = CitizenFX.Core.Control.SelectCharacterMichael; //F5 default
            MenuCheckboxItem box = new MenuCheckboxItem(
                                    "Left align menu",
                                    "Move this menu to the left side of the screen if enabled",
                                    menu.LeftAligned
                                    );

            MenuCheckboxItem toggleSub = new MenuCheckboxItem("Share my waypoint", "Share your current waypoint with other players.  Will update whenever you change your waypoint, until disabled", false);
            menu.AddMenuItem(toggleSub);

            pubsMenu = new Menu("Shared Waypoints", "Choose a player to follow");
            MenuController.AddSubmenu(menu, pubsMenu);
            pubsMenuItem = new MenuItem(
                                "Follow another player's shared waypoint",
                                "Select this option to see a list of players who are currently sharing their waypoint.  Choose a player to follow their waypoint."
                                );
            menu.AddMenuItem(pubsMenuItem);
            MenuController.BindMenuItem(menu, pubsMenu, pubsMenuItem);

            unsub = new MenuItem("Unfollow", "Select this to stop following a shared waypoint") {
                Enabled = false
            };
            menu.AddMenuItem(unsub);
            menu.AddMenuItem(box);

            menu.OnCheckboxChange += (_menu, _item, _index, _checked) => {
                Debug.WriteLine($"OnCheckboxChange: [{_menu}, {_item}, {_index}, {_checked}]");
                if (_item == box) {
                    if (_checked) {
                        MenuController.MenuAlignment = MenuController.MenuAlignmentOption.Left;
                    }
                    else {
                        MenuController.MenuAlignment = MenuController.MenuAlignmentOption.Right;
                    }
                }
                else if (_item == toggleSub) {
                    if (!publishing) {
                        var blipenum = GetWaypointBlipEnumId();
                        var bliphandle = GetFirstBlipInfoId(blipenum);
                        var blipcoords = GetBlipCoords(bliphandle);
                        coords = blipcoords;
                        publishing = true;
                        _ = WaypointPublisher();
                        TriggerServerEvent("SharedWaypoint:RegisterPublisher", blipcoords);
                    }
                    else {
                        TriggerServerEvent("SharedWaypoint:UnregisterPublisher");
                        publishing = false;
                    }
                }
            };


            pubsMenu.OnMenuOpen += (_menu) => {
                pubsMenu.ClearMenuItems();
                TriggerServerEvent("SharedWaypoint:GetActivePublishers");
            };

            menu.OnItemSelect += (_menu, _item, _index) => {
                WriteDebug($"OnItemSelect triggered for _item.Text: {_item.Text}, ItemData: {_item.ItemData} ");
                if (_item == unsub) {
                    TriggerServerEvent("SharedWaypoint:Unsubscribe");
                    ClearWaypoint();
                    unsub.Enabled = false;
                    unsub.Description = "Select this to stop following a shared waypoint";
                    menu.MenuSubtitle = "Status: Not following anyone";
                    pubsMenuItem.Enabled = true;
                }
            };

            pubsMenu.OnItemSelect += (_menu, _item, _index) => {
                WriteDebug($"OnItemSelect triggered for _item.Text: {_item.Text}, ItemData: {_item.ItemData} ");
                if (_item.ItemData != null) {
                    WriteDebug($"Triggering Subscribe event with ItemData:{_item.ItemData}");
                    TriggerServerEvent("SharedWaypoint:Subscribe", _item.ItemData);
                    _item.Description = $"You are now following waypoint updates from {_item.Text}";
                    unsub.Enabled = true;
                    unsub.Description = $"Select to stop following waypoint updates from {_item.Text}";
                    menu.MenuSubtitle = $"Following {_item.Text}";
                    pubsMenuItem.Enabled = false;
                }
            };

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
                var blipenum = GetWaypointBlipEnumId();
                var bliphandle = GetFirstBlipInfoId(blipenum);
                var blipcoords = GetBlipCoords(bliphandle);
                if (blipcoords != coords) {
                    TriggerServerEvent("SharedWaypoint:Publish", blipcoords);
                    coords = blipcoords;
                }
            }
            WriteDebug("WaypointPublisher started");
            while (publishing) {
                PollWaypoint();
                await Delay(500); //todo: cancellation token
            }
            WriteDebug("WaypointPublisher concluded");
        }

        private void WriteDebug(string message) {
            Debug.WriteLine(message);
        }

        private void ReceivePublisher(int pubID, string name) {
            MenuItem newitem = new MenuItem(name, $"{name} is sharing their waypoint.  Select to follow it") {
                ItemData = pubID
            };
            pubsMenu.AddMenuItem(newitem);
            pubsMenu.RefreshIndex();
        }

        private void ForceUnfollow() {
            ClearWaypoint();
            pubsMenuItem.Enabled = true;
            unsub.Enabled = false;
            unsub.Description = "Select this to stop following a shared waypoint";
            menu.MenuSubtitle = "Status: Not following anyone";
        }
    }
}