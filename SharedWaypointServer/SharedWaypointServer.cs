using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace SharedWaypointServer {
    
    public class PublisherInfo {
        public PublisherInfo(Player publisher, Vector3 coords) {
            this.coords = coords;
            this.subscribers = new List<Player>();
            this.publisher = publisher;
        }
        public Vector3 coords { get; set; }
        public List<Player> subscribers;
        public Player publisher { get; }
    }
    public class SharedWaypointServer : BaseScript {

        private Dictionary<int, PublisherInfo> Publishers = new Dictionary<int, PublisherInfo>();
        private Dictionary<Player, Player> Subscriptions = new Dictionary<Player, Player>(); //key is a subscriber, value is the publisher they are subscribed to

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
            if (Publishers.ContainsKey(source.GetHashCode())) {
                ClientTrace(source, "You triggered RegisterPublisher when you are already publishing");
            }
            else {
                PublisherInfo info = new PublisherInfo(source, coords);
                Publishers.Add(source.GetHashCode(), info);
                ClientTrace(source, $"You triggered RegisterPublisher with the coords: {coords}");
            }
        }

        private void Publish([FromSource] Player source, Vector3 coords) {
            if (!Publishers.ContainsKey(source.GetHashCode())) {
                ClientTrace(source, $"Publish triggered from invalid source player {source.Name} with coords {coords}, this player is not a registered publisher");
                return;
            }
            Publishers[source.GetHashCode()].coords = coords;

            foreach (Player subscriber in Publishers[source.GetHashCode()].subscribers) {
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
            if (!Publishers.ContainsKey(source.GetHashCode())) {
                ClientTrace(source, $"UnregisterPublisher triggered from invalid source, {source.Name} is not a registered publisher");
                return;
            }

            foreach (Player subscriber in Publishers[source.GetHashCode()].subscribers) {
                TriggerClientEvent(subscriber, "SharedWaypoint:ForceUnfollow");
            }
            
            Publishers.Remove(source.GetHashCode());
            ClientTrace(source, $"UnregisterPublisher has removed {source.Name} from the publishers list");
        }

        private void GetActivePublishers([FromSource] Player source) {
            foreach(int playerHash in Publishers.Keys) {
                TriggerClientEvent(source, "SharedWaypoint:ReceivePublisher", playerHash, Publishers[playerHash].publisher.Name);
            }   
        }

        private void Subscribe([FromSource] Player source, int publisherId) {
            if (Publishers[publisherId].subscribers.Contains(source)) {
                ClientTrace(source, $"Subscribe event triggered unnecessarily, {source.Name} is already subscribed to {Publishers[publisherId].publisher.Name}");
                return;
            }
            Publishers[publisherId].subscribers.Add(source);
            Subscriptions[source] = Publishers[publisherId].publisher;
            if (Publishers[publisherId].coords != new Vector3(0, 0, 0)) {
                TriggerClientEvent(source, "SharedWaypoint:SetWaypoint", Publishers[publisherId].coords);
            }
        }

        private void Unsubscribe([FromSource] Player source) {
            Player publisher = Subscriptions[source];
            Publishers[publisher.GetHashCode()].subscribers.Remove(source);
            Subscriptions[source] = null;
            ClientTrace(source, $"Unsubscribing {source.Name} from {publisher.Name}");
        }
    }
}