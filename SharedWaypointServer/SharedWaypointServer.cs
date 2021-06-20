using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace SharedWaypointServer {
    
    public class PublisherInfo {
        public PublisherInfo(Vector3 coords) {
            this.coords = coords;
            this.subscribers = new List<Player>();
        }
        public Vector3 coords { get; set; }
        public List<Player> subscribers;
    }
    public class SharedWaypointServer : BaseScript {

        private Dictionary<Player, PublisherInfo> Publishers = new Dictionary<Player, PublisherInfo>();
        private Dictionary<int, Player> PublisherRefs = new Dictionary<int, Player>();
        private Dictionary<Player, Player> Subscriptions = new Dictionary<Player, Player>(); //key is subscriber, value is publisher

        /*
         * can probably just remove this?  it was for CLI testing.  have since switched to GUI
        private void PrintToClientChat(Player player, string message) { 
            //print a chat message on the client side for a specific player
            TriggerClientEvent(player, "chat:addMessage", new {
                color = new[] { 255, 0, 0 },
                args = new[] { "[SharedWaypointClient]", message }
            });
        }
        */
        public SharedWaypointServer() {
            EventHandlers["SharedWaypoint:RegisterPublisher"] += new Action<Player, Vector3>(RegisterPublisher);
            EventHandlers["SharedWaypoint:Publish"] += new Action<Player, Vector3>(Publish);
            EventHandlers["SharedWaypoint:UnregisterPublisher"] += new Action<Player>(UnregisterPublisher);

            EventHandlers["SharedWaypoint:GetActivePublishers"] += new Action<Player>(GetActivePublishers);
            EventHandlers["SharedWaypoint:Subscribe"] += new Action<Player, int>(Subscribe);
            EventHandlers["SharedWaypoint:Unsubscribe"] += new Action<Player>(Unsubscribe);
        }

        private void ClientTrace(Player player, string message) {
            TriggerClientEvent(player, "SharedWaypoint:Trace", message);
        }

        private void RegisterPublisher([FromSource] Player source, Vector3 coords) {
            if (Publishers.ContainsKey(source)) {
                Publishers[source].coords = coords;
                ClientTrace(source, $"You triggered RegisterPublisher to update with the coords: {coords}");
            }
            else {
                PublisherInfo info = new PublisherInfo(coords);
                Publishers.Add(source, info);
                PublisherRefs[source.GetHashCode()] = source;
                ClientTrace(source, $"You triggered RegisterPublisher with the coords: {coords}");
            }
        }

        private void Publish([FromSource] Player source, Vector3 coords) {
            if (!Publishers.ContainsKey(source)) {
                ClientTrace(source, $"Publish triggered from invalid source player {source.Name} with coords {coords}, this player is not a registered publisher");
                return;
            }
            Publishers[source].coords = coords;

            foreach (Player subscriber in Publishers[source].subscribers) {
                if (coords == new Vector3(0, 0, 0)) {
                    TriggerClientEvent(subscriber, "SharedWaypoint:ClearWaypoint", coords);
                }
                else {
                    TriggerClientEvent(subscriber, "SharedWaypoint:SetWaypoint", coords);
                }
            }
            ClientTrace(source, $"Publish triggered by {source.Name}, updated coords to {coords}");
        }

        private void UnregisterPublisher([FromSource] Player source) {
            if (!Publishers.ContainsKey(source)) {
                ClientTrace(source, $"UnregisterPublisher triggered from invalid source, {source.Name} is not a registered publisher");
                return;
            }

            foreach (Player subscriber in Publishers[source].subscribers) {
                TriggerClientEvent(subscriber, "SharedWaypoint:ForceUnfollow");
            }
            
            Publishers.Remove(source);
            PublisherRefs.Remove(source.GetHashCode());
            ClientTrace(source, $"UnregisterPublisher has removed {source.Name} from the publishers list");
        }

        private void GetActivePublishers([FromSource] Player source) {
            /* left over from early CLI version, PublisherRefs was a list, should probably just delete this
             * for(int i=0; i < PublisherRefs.Count; i++) {
                Player player = PublisherRefs[i];
                _msg(source, $"[{i}] {player.Name} is publishing waypoints.");
                //_chatmsg(source, $"The publisher {player.Name} has {Publishers[player].subscribers.Count} subscribers");
            }*/
            foreach(Player player in Publishers.Keys) {
                TriggerClientEvent(source, "SharedWaypoint:ReceivePublisher", player.GetHashCode(), player.Name);
            }   
        }

        private void Subscribe([FromSource] Player source, int publisherId) {
            Player publisher = PublisherRefs[publisherId];
            if (Publishers[publisher].subscribers.Contains(source)) {
                ClientTrace(source, $"Subscribe event triggered unnecessarily, {source.Name} is already subscribed to {publisher.Name}");
                return;
            }
            Publishers[publisher].subscribers.Add(source);
            Subscriptions[source] = publisher;
            if (Publishers[publisher].coords != new Vector3(0, 0, 0)) {
                TriggerClientEvent(source, "SharedWaypoint:SetWaypoint", Publishers[publisher].coords);
            }
        }

        private void Unsubscribe([FromSource] Player source) {
            Player publisher = Subscriptions[source];
            Publishers[publisher].subscribers.Remove(source);
            Subscriptions[source] = null;
            ClientTrace(source, $"Unsubscribing {source.Name} from {publisher.Name}");
        }
    }
}