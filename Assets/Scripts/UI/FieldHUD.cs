using TMPro;
using HuntHappens.Interaction;
using UnityEngine;
using HuntHappens.Player;
using HuntHappens.SmallGame;
using HuntHappens.Weapons;
using HuntHappens.Traps;
using UnityEngine.UI;
namespace HuntHappens.UI
{
    public sealed class FieldHUD : MonoBehaviour
    {
        static readonly Color Ivory = new Color(0.96f,0.92f,0.79f);
        static readonly Color Orange = new Color(1f,0.37f,0.13f);
        TMP_FontAsset font;
        RatHunting hunt; PrototypeInput input; PlayerHealth health; TranquilizerGun gun; TrapPlacement traps;
        RectTransform root; GameObject inventory;
        TextMeshProUGUI money, total, selectedName;
        DeerRewardEffect rewardEffect;
        readonly TextMeshProUGUI[] counts = new TextMeshProUGUI[4], supplies = new TextMeshProUGUI[4], catchLabels = new TextMeshProUGUI[3];
        readonly UnityEngine.UI.Image[] slotIcons = new UnityEngine.UI.Image[4];
        readonly UnityEngine.UI.Image[] baitIcons = new UnityEngine.UI.Image[4];
        readonly SketchPanel[] slots = new SketchPanel[4];
        UnityEngine.UI.Image healthFill;
        readonly Sprite[] sprites = new Sprite[9];
        public bool IsOpen => inventory != null && inventory.activeSelf;
        public void Build(Transform parent, RatHunting hunting, TMP_FontAsset typeface, Texture2D atlas)
        {
            hunt = hunting; input = hunt.GetComponent<PrototypeInput>(); health = hunt.GetComponent<PlayerHealth>();
            gun = hunt.GetComponent<TranquilizerGun>(); traps = hunt.GetComponent<TrapPlacement>();font = typeface;
            root = Rect("Field HUD", parent, Vector2.zero, Vector2.zero);Stretch(root);
            if(atlas != null)
            {
                // Authored sheet bounds (top-left origin), preserving the long rifle silhouette.
                Rect[] regions={new Rect(0,100,515,300),new Rect(515,45,275,370),new Rect(795,85,480,330),new Rect(0,470,442,330),new Rect(445,470,410,330),new Rect(855,460,420,340),new Rect(0,875,425,340),new Rect(425,875,420,340),new Rect(845,865,430,350)};
                for(int i=0;i<9;i++){var r=regions[i];float scale=atlas.width/1280f;sprites[i]=Sprite.Create(atlas,new Rect(r.x*scale,(1280-r.y-r.height)*scale,r.width*scale,r.height*scale),Vector2.one*0.5f,100);}
            }
            var healthPanel = Panel("Health",root,new Vector2(235,72),new Vector2(410,35));Anchor(healthPanel.rectTransform,new Vector2(0,0));
            healthFill=Image("Health Fill",healthPanel.transform,new Vector2(4,0),new Vector2(402,27),null);
            healthFill.rectTransform.anchorMin=healthFill.rectTransform.anchorMax=new Vector2(0,0.5f);
            healthFill.rectTransform.pivot=new Vector2(0,0.5f);
            healthFill.color=Orange;
            var wallet=Panel("Wallet",root,new Vector2(-115,-65),new Vector2(170,65));Anchor(wallet.rectTransform,new Vector2(1,1));
            money=Label("Balance",wallet.transform,Vector2.zero,new Vector2(160,55),40,"");
            for(int i=0;i<4;i++)
            {
                slots[i]=Panel("Quick Slot "+(i+1),root,new Vector2((i-1.5f)*108,85),new Vector2(96,94));Anchor(slots[i].rectTransform,new Vector2(0.5f,0));
                slotIcons[i]=Image("Icon",slots[i].transform,new Vector2(0,5),new Vector2(82,65),null);
                baitIcons[i]=Image("Bait Icon",slots[i].transform,new Vector2(-16,-27),new Vector2(36,26),sprites[4]);
                baitIcons[i].gameObject.SetActive(false);
                counts[i]=Label("Count",slots[i].transform,new Vector2(28,-28),new Vector2(40,28),26,"");
                Label("Key",slots[i].transform,new Vector2(0,-65),new Vector2(40,32),29,(i+1).ToString());
            }
            selectedName=Label("Selected Tool",root,new Vector2(0,177),new Vector2(850,75),26,"");Anchor(selectedName.rectTransform,new Vector2(0.5f,0));
            var window=Panel("Inventory",root,new Vector2(0,50),new Vector2(1100,640));inventory=window.gameObject;window.color=new Color(0.055f,0.065f,0.035f,0.92f);
            Label("Title",window.transform,new Vector2(-330,265),new Vector2(370,65),50,"РЮКЗАК");
            var closeRect=Rect("Close",window.transform,new Vector2(360,265),new Vector2(310,50));
            var closeImage=closeRect.gameObject.AddComponent<UnityEngine.UI.Image>();closeImage.color=new Color(0,0,0,0.01f);
            var close=closeRect.gameObject.AddComponent<UnityEngine.UI.Button>();close.targetGraphic=closeImage;close.onClick.AddListener(()=>SetOpen(false));
            Label("Caption",closeRect,Vector2.zero,new Vector2(300,50),29,"Закрыть");
            Label("Supplies",window.transform,new Vector2(-350,200),new Vector2(330,40),34,"ПРИПАСЫ").color=new Color(0.73f,0.8f,0.55f);
            int[] icons={4,5,3,2};
            for(int i=0;i<4;i++)
            {
                var card=Panel("Supply "+i,window.transform,new Vector2((i-1.5f)*255,82),new Vector2(235,180));
                Image("Icon",card.transform,new Vector2(0,28),new Vector2(180,104),sprites[icons[i]]);
                supplies[i]=Label("Quantity",card.transform,new Vector2(0,-57),new Vector2(222,50),31,"");
            }
            Label("Catch",window.transform,new Vector2(-350,-41),new Vector2(330,40),34,"УЛОВ").color=new Color(0.73f,0.8f,0.55f);
            for(int i=0;i<3;i++)
            {
                var card=Panel("Catch "+i,window.transform,new Vector2((i-1)*345,-157),new Vector2(325,175));
                Image("Icon",card.transform,new Vector2(-67,8),new Vector2(180,138),sprites[6+i]);
                catchLabels[i]=Label("Quantity and Value",card.transform,new Vector2(94,0),new Vector2(135,130),29,"");
            }
            total=Label("Total Value",window.transform,new Vector2(220,-283),new Vector2(620,46),34,"");total.alignment=TextAlignmentOptions.Right;
            inventory.SetActive(false);input.InventoryToggleRequested+=Toggle;
            rewardEffect = root.gameObject.AddComponent<DeerRewardEffect>();
            rewardEffect.Build(root, wallet.rectTransform, font, hunt.shop.Wallet, hunt);
        }
        public void Toggle() => SetOpen(!IsOpen);
        public void SetOpen(bool open)
        {
            if(input == null || open == IsOpen) return;
            if(open && (input.GameplayBlocked || hunt.shop.IsOpen))return;
            inventory.SetActive(open);input.SetGameplayBlocked(open);
        }
        public void Refresh()
        {
            if(hunt == null)return;
            if(IsOpen && input.MenuClosePressed)SetOpen(false);
            var bag = hunt.GetComponent<PlayerInventory>();
            int selected = bag.HandSlot;
            int baitedCages = bag.HasUnassignedItem && HasBait(bag.Hand) ? bag.Hand.quantity : 0;
            for (int i = 0; i < 4; i++)
            {
                var entry = bag.Slot(i);
                slotIcons[i].enabled = entry != null;
                if (entry != null) slotIcons[i].sprite = sprites[entry.Icon];
                bool baited = HasBait(entry);
                baitIcons[i].gameObject.SetActive(baited);
                if (baited) baitedCages += entry.quantity;
                counts[i].text = entry?.kind == InventoryKind.Gun ? hunt.Darts.ToString() : baited ? "1" : entry != null && entry.quantity > 1 ? entry.quantity.ToString() : "";
                counts[i].color = baited ? Orange : Ivory;
                slots[i].ink = i == selected ? Orange : new Color(0.9f,0.86f,0.69f,0.55f);
                slots[i].thickness = i == selected ? 3 : 1.5f; slots[i].SetVerticesDirty();
            }
            selectedName.text = bag.Hand != null ? bag.Hand.Title + (bag.HasUnassignedItem ? " · в руках" : "") : "";
            int availableTraps = bag.Count(InventoryKind.Trap);
            int current=health != null?health.Current:100;
            healthFill.rectTransform.sizeDelta=new Vector2(402*Mathf.Clamp01(current/100f),27);
            healthFill.color=current<=25?Color.red:Orange;
            money.text="$"+hunt.shop.Wallet.Balance;
            supplies[0].text="Хлеб ×"+hunt.Bread;supplies[1].text="Дротики ×"+hunt.Darts;supplies[2].text="Клетки ×"+hunt.Available;supplies[3].text="Ловушки ×"+availableTraps;
            if (baitedCages > 0) supplies[2].text += "\nС приманкой ×" + baitedCages;
            supplies[2].fontSize = baitedCages > 0 ? 24 : 31;
            supplies[2].rectTransform.sizeDelta = new Vector2(222, 62);
            catchLabels[0].text="Чёрная ×"+(hunt.Rats-hunt.GreyRats-hunt.LargeRats)+"\n<color=#FF812F>$10 / шт.</color>";
            catchLabels[1].text="Серая ×"+hunt.GreyRats+"\n<color=#FF812F>$15 / шт.</color>";
            catchLabels[2].text="Крупная ×"+hunt.LargeRats+"\n<color=#FF812F>$25 / шт.</color>";
            total.text="Стоимость улова: <color=#FF812F>$"+hunt.SaleTotal+"</color>";
        }
        public void Hide(){if(IsOpen)SetOpen(false);}
        static bool HasBait(InventoryEntry entry) => entry?.kind == InventoryKind.Cage && entry.cageBaited && entry.cageRat == 0;
        void OnDestroy(){if(input!=null){input.InventoryToggleRequested-=Toggle;if(IsOpen)input.SetGameplayBlocked(false);}foreach(var sprite in sprites)if(sprite!=null)Destroy(sprite);}
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        static void Anchor(RectTransform r,Vector2 a){r.anchorMin=r.anchorMax=a;}
        RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*0.5f;r.anchoredPosition=pos;r.sizeDelta=size;return r;}
        SketchPanel Panel(string name,Transform parent,Vector2 pos,Vector2 size)
        {var r=Rect(name,parent,pos,size);var panel=r.gameObject.AddComponent<SketchPanel>();panel.color=new Color(0.05f,0.065f,0.035f,0.65f);panel.raycastTarget=false;return panel;}
        TextMeshProUGUI Label(string name,Transform parent,Vector2 pos,Vector2 size,float fontSize,string text)
        {var r=Rect(name,parent,pos,size);var label=r.gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.fontSize=fontSize;label.color=Ivory;label.alignment=TextAlignmentOptions.Center;label.text=text;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;return label;}
        UnityEngine.UI.Image Image(string name,Transform parent,Vector2 pos,Vector2 size,Sprite sprite)
        {var r=Rect(name,parent,pos,size);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;return image;}
    }
}
