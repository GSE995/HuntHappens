using UnityEngine;
namespace HuntHappens.SmallGame
{
    public sealed class RatCage : MonoBehaviour
    {
        public Transform door;
        public GameObject bait;
        public RatHunting Owner { get; set; }
        public RatAI Captive { get; private set; }
        public bool Baited { get; private set; }
        private bool recovered;
        public string Prompt => Captive != null ? (Captive.isAggressive ? "Крупная крыса — $25" : Captive.isGrey ? "Серая крыса — $15" : "Чёрная крыса — $10") + "\nE — забрать крысу и клетку" : Baited ? "Ждём крысу. Отойдите на 3 м | E — забрать клетку" : "E — положить хлеб | без хлеба: забрать клетку";
        public bool Interact(RatHunting player)
        {
            if (recovered || player == null || player != Owner) return false;
            if (!Baited && Captive == null && player.Bread > 0)
            { player.UseBread(); Baited = true; bait.SetActive(true); return true; }
            recovered = true;
            bool caught = Captive != null;
            if (caught) Captive.ReleaseForRespawn();
            player.Recover(caught, caught && Captive.isGrey, caught && Captive.isAggressive); gameObject.SetActive(false); Destroy(gameObject); return true;
        }
        public bool TryCapture(RatAI rat)
        {
            if (recovered || !Baited || Captive != null || rat == null || rat.Caught || Vector3.Distance(rat.transform.position, transform.position) > 0.45f) return false;
            Captive = rat; Baited = false; bait.SetActive(false);
            door.localPosition = new Vector3(0, 0.25f, 0.44f);
            rat.Capture(this); return true;
        }
    }
}
