using UnityEngine;

namespace HuntHappens.Characters
{
    [CreateAssetMenu(fileName = "NewDialogue", menuName = "Hunt Happens/Dialogue")]
    public sealed class DialogueData : ScriptableObject
    {
        [SerializeField] private string speakerName = "Персонаж";
        [SerializeField, TextArea(2, 5)] private string[] lines = System.Array.Empty<string>();
        [SerializeField] private string finalButtonLabel = "Завершить";
        [SerializeField] private string repeatButtonLabel = "До встречи";

        public string SpeakerName => speakerName;
        public int LineCount => lines != null ? lines.Length : 0;
        public string GetLine(int index) => lines[index];
        public string FinalButtonLabel => finalButtonLabel;
        public string RepeatButtonLabel => repeatButtonLabel;
    }
}
