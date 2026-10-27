using HuntHappens.Player;
using TMPro;
using UnityEngine;

namespace HuntHappens.Characters
{
    [DisallowMultipleComponent]
    public sealed class FarmerDialogue : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset dialogueFont;
        [SerializeField] private DialogueData dialogue;
        private PrototypeInput input;
        private GameObject root;
        private TMP_Text body, nextLabel, counter;
        private int index;
        public bool IsOpen => root != null && root.activeSelf;
        public bool QuestAccepted { get; private set; }
        public int LineIndex => index;
        public int LineCount => dialogue != null ? dialogue.LineCount : 0;
        public string CurrentLine => LineCount > 0 ? dialogue.GetLine(Mathf.Clamp(index, 0, LineCount - 1)) : string.Empty;
        public event System.Action HelpAccepted;

        public bool TryOpen(PrototypeInput player, TMP_FontAsset font)
        {
            if (!isActiveAndEnabled || IsOpen || player == null || player.GameplayBlocked || LineCount == 0) return false;
            index = Mathf.Clamp(index, 0, LineCount - 1);
            input = player;
            if (root == null) Build(dialogueFont != null ? dialogueFont : font);
            input.SetGameplayBlocked(true);
            root.SetActive(true);
            Refresh();
            return true;
        }

        public void Advance()
        {
            if (!IsOpen) return;
            if (index < LineCount - 1) { index++; Refresh(); return; }
            bool firstAcceptance = !QuestAccepted;
            QuestAccepted = true;
            Close();
            if (firstAcceptance) HelpAccepted?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            if (input != null) input.SetGameplayBlocked(false);
            input = null;
        }

        private void Update()
        {
            if (!IsOpen) return;
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
#endif
        }
        private void OnDisable() => Close();
        private void Refresh()
        {
            body.text = CurrentLine;
            counter.text = $"{index + 1} / {LineCount}    •    Esc — закрыть";
            nextLabel.text = index == LineCount - 1 ? (QuestAccepted ? dialogue.RepeatButtonLabel : dialogue.FinalButtonLabel) : "Далее  →";
        }

        private void Build(TMP_FontAsset font)
        {
            var panel = new DialoguePanel(transform, font, dialogue.SpeakerName, Advance);
            root = panel.root; body = panel.body; counter = panel.counter; nextLabel = panel.nextLabel;
        }
    }
}