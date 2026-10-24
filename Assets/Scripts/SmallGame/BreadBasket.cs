using UnityEngine;
namespace HuntHappens.SmallGame
{
    public sealed class BreadBasket : MonoBehaviour
    {
        private float nextBread;
        public string Prompt => Time.time >= nextBread ? "E — взять бесплатный хлеб" : "Хлеб появится через " + Mathf.CeilToInt(nextBread - Time.time) + " с";
        public bool Take(RatHunting player)
        { if (player == null || Time.time < nextBread || !player.CollectBread()) return false; nextBread = Time.time + 30; return true; }
    }
}
