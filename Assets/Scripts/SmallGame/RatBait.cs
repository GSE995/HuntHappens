using HuntHappens.Interaction;

namespace HuntHappens.SmallGame
{
    public static class RatBait
    {
        public static bool IsBait(InventoryKind kind) => kind == InventoryKind.Bread ||
            kind == InventoryKind.CheeseSandwich || kind == InventoryKind.Latte;
        public static InventoryKind ForRat(int kind) => kind == 0 ? InventoryKind.Bread :
            kind == 1 ? InventoryKind.CheeseSandwich : kind == 2 ? InventoryKind.Latte : InventoryKind.None;
        public static string Name(InventoryKind kind) => kind == InventoryKind.Bread ? "Хлеб" :
            kind == InventoryKind.CheeseSandwich ? "Сыр" : kind == InventoryKind.Latte ? "Латте" : "Нет приманки";
        public static string Description(InventoryKind kind) => kind == InventoryKind.Bread ? "Обычная (чёрная) крыса ловится только на хлеб" :
            kind == InventoryKind.CheeseSandwich ? "Серая крыса ловится только на сыр" :
            kind == InventoryKind.Latte ? "Городская крыса: на людей насмотрелась, теперь ловится только на латте" : "";
        public const string Preferences = "Обычная — хлеб; серая — сыр; большая — латте";
    }
}
