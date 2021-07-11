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
            Coords = coords;
            Subscribers = new List<Player>();
            Publisher = publisher;
        }
        public Vector3 Coords { get; set; }
        public List<Player> Subscribers;
        public Player Publisher { get; }
    }
    public class SharedWaypointServer : BaseScript {

        private Dictionary<int, PublisherInfo> publishers = new Dictionary<int, PublisherInfo>(); //key is a publishers Player hash, value is their PublisherInfo
        private Dictionary<Player, Player> subscriptions = new Dictionary<Player, Player>(); //key is a subscriber, value is the publisher they are subscribed to
        private List<Player> players = new List<Player>(); 

        public SharedWaypointServer() {
            EventHandlers["SharedWaypoint:RegisterPublisher"] += new Action<Player, Vector3>(RegisterPublisher);
            EventHandlers["SharedWaypoint:Publish"] += new Action<Player, Vector3>(Publish);
            EventHandlers["SharedWaypoint:UnregisterPublisher"] += new Action<Player>(UnregisterPublisher);

            EventHandlers["SharedWaypoint:GetActivePublishers"] += new Action<Player>(GetActivePublishers);
            EventHandlers["SharedWaypoint:Subscribe"] += new Action<Player, int>(Subscribe);
            EventHandlers["SharedWaypoint:Unsubscribe"] += new Action<Player>(Unsubscribe);
            
            EventHandlers["playerDropped"] += new Action<Player, string>(OnPlayerDropped);
        }
        
        private void OnPlayerDropped([FromSource] Player source, string reason) {
            Debug.WriteLine($"playerDropped event handled for Player: {source.Name}.  Reason: {reason}");
            if (publishers.ContainsKey(source.GetHashCode())) {
                UnregisterPublisher(source);
            }
            if (subscriptions.ContainsKey(source)) {
                Unsubscribe(source);
            }
        }

        private void ClientTrace(Player player, string message) {
            TriggerClientEvent(player, "SharedWaypoint:Trace", message);
        }

        private void RegisterPublisher([FromSource] Player source, Vector3 coords) {
            if (publishers.ContainsKey(source.GetHashCode())) {
                ClientTrace(source, "You triggered RegisterPublisher when you are already publishing");
            }
            else {
                PublisherInfo info = new PublisherInfo(source, coords);
                publishers.Add(source.GetHashCode(), info);
                foreach (Player player in players) {
                    TriggerClientEvent(player, "SharedWaypoint:ReceivePublisher", source.GetHashCode(), source.Name);
                }
                ClientTrace(source, $"You triggered RegisterPublisher with the coords: {coords}");
            }
        }

        private void Publish([FromSource] Player source, Vector3 coords) {
            if (!publishers.ContainsKey(source.GetHashCode())) {
                ClientTrace(source, $"Publish triggered from invalid source player {source.Name} with coords {coords}, this player is not a registered publisher");
                return;
            }
            publishers[source.GetHashCode()].Coords = coords;

            foreach (Player subscriber in publishers[source.GetHashCode()].Subscribers) {
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
            if (!publishers.ContainsKey(source.GetHashCode())) {
                Debug.WriteLine($"UnregisterPublisher triggered for invalid source, {source.Name} is not a registered publisher");
                return;
            }
            foreach (Player subscriber in publishers[source.GetHashCode()].Subscribers) {
                TriggerClientEvent(subscriber, "SharedWaypoint:ForceUnfollow");
            }
            foreach (Player player in players) {
                TriggerClientEvent(player, "SharedWaypoint:RemovePublisher", source.GetHashCode());
            }
            publishers.Remove(source.GetHashCode());
            Debug.WriteLine($"UnregisterPublisher has removed {source.Name} from the publishers list");
        }

        private void GetActivePublishers([FromSource] Player source) {
            if (!players.Contains(source)) {
                players.Add(source);
            }
            foreach (int playerHash in publishers.Keys) {
                TriggerClientEvent(source, "SharedWaypoint:ReceivePublisher", playerHash, publishers[playerHash].Publisher.Name);
            }   
        }

        private void Subscribe([FromSource] Player source, int publisherId) {
            if (publishers[publisherId].Subscribers.Contains(source)) {
                ClientTrace(source, $"Subscribe event triggered unnecessarily, {source.Name} is already subscribed to {publishers[publisherId].Publisher.Name}");
                return;
            }
            publishers[publisherId].Subscribers.Add(source);
            subscriptions[source] = publishers[publisherId].Publisher;
            if (publishers[publisherId].Coords != new Vector3(0, 0, 0)) {
                TriggerClientEvent(source, "SharedWaypoint:SetWaypoint", publishers[publisherId].Coords);
            }
            TriggerClientEvent(publishers[publisherId].Publisher, "SharedWaypoint:UpdateFollowerCount", publishers[publisherId].Subscribers.Count);
        }

        private void Unsubscribe([FromSource] Player source) {
            if (!subscriptions.ContainsKey(source)) {
                Debug.WriteLine($"Unsubscribe invoked for subscriber {source.Name} who has no active subscription, doing nothing");
                return;
            }
            Player publisher = subscriptions[source];
            subscriptions.Remove(source);
            Debug.WriteLine($"Unsubscribe: removed {source.Name} from subscriptions list");
            if (!publishers.ContainsKey(publisher.GetHashCode())) {
                Debug.WriteLine($"Unsubscribe invoked for subscriber {source.Name} but their publisher {publisher.Name} is no longer publishing");
                return;
            }
            publishers[publisher.GetHashCode()].Subscribers.Remove(source);
            Debug.WriteLine($"Unsubscribe: removed {source.Name} from the subscriber list for {publisher.Name}");
        }
    }
}