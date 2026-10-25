using HuntHappens.Animals;
using HuntHappens.Shop;
using UnityEngine;

namespace HuntHappens.Delivery
{
    public sealed class DeerEnclosure : MonoBehaviour
    {
        [SerializeField] private DeerSedation displayPrefab;
        [SerializeField] private Transform[] restingPlaces;
        [SerializeField] private AudioClip successSound;
        private int accepted;
        public int AcceptedCount => accepted;
        private void Start()
        {
            accepted = Mathf.Max(0, ShopProgress.Load().acceptedDeer);
            if (FindAnyObjectByType<DeerCaptureZone>() != null) { accepted = (ShopProgress.Load().returnedDeerIds ?? new string[0]).Length; return; }
            if (displayPrefab == null || restingPlaces == null) return;
            for (int i = 0; i < Mathf.Min(accepted, restingPlaces.Length); i++)
                Place(Instantiate(displayPrefab), i);
        }
        public void Accept(DeerSedation deer)
        {
            int slot = accepted++;
            Place(deer, slot);
            // Use the existing snapshot; the wallet event saves its new balance immediately after this.
            var data = ShopProgress.Load();
            data.acceptedDeer = accepted;
            PlayerPrefs.SetString(ShopProgress.Key, JsonUtility.ToJson(data));
            if (successSound != null) AudioSource.PlayClipAtPoint(successSound, transform.position);
        }
        public void RestoreAnimal(DeerSedation deer, int index) => Place(deer, index);
        private void Place(DeerSedation deer, int index)
        {
            if (restingPlaces == null || index >= restingPlaces.Length)
            { deer.gameObject.SetActive(false); Destroy(deer.gameObject); return; }
            var place = restingPlaces[index];
            deer.transform.SetParent(transform, true);
            deer.PlaceInEnclosure(place.position, place.rotation);
            deer.name = "Accepted Deer " + (index + 1);
        }
    }
}
