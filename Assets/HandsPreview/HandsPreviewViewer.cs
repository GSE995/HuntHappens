using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class HandsPreviewViewer : MonoBehaviour
{
    public Animator target;
    public AnimationClip[] clips;
    public Camera viewCamera;
    PlayableGraph graph;
    AnimationClipPlayable playable;
    int selected;
    float time, speed = 1;
    bool paused;
    Vector3 focus = new Vector3(0, .052f, 0);
    readonly string[] captions = { "Idle", "Grab", "Hold", "Throw", "Weapon", "Shoot", "Trap" };
    void Start() { Select(0); SetView(0); Debug.Log("HANDS_PREVIEW_PLAYING: " + clips.Length + " clips ready"); }
    void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
    public void Select(int index)
    {
        if (clips == null || clips.Length == 0) return;
        selected = Mathf.Clamp(index, 0, clips.Length - 1); time = 0;
        if (graph.IsValid()) graph.Destroy();
        graph = PlayableGraph.Create("Hands preview"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        playable = AnimationClipPlayable.Create(graph, clips[selected]);
        playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
        var output = AnimationPlayableOutput.Create(graph, "Hands", target); output.SetSourcePlayable(playable);
        graph.Play(); playable.SetTime(0); graph.Evaluate(0);
    }
    void Update()
    {
        if (!graph.IsValid()) return;
        if (!paused) time = Mathf.Repeat(time + Time.unscaledDeltaTime * speed, Mathf.Max(.01f, clips[selected].length));
        playable.SetTime(time); graph.Evaluate(0);
    }
    void SetView(int view)
    {
        Vector3 offset = view == 1 ? new Vector3(.45f, .21f, .58f) : view == 2 ? new Vector3(0, .18f, -.67f) : new Vector3(0, .228f, .65f);
        viewCamera.transform.position = focus + offset; viewCamera.transform.LookAt(focus);
    }
    void OnGUI()
    {
        float scale = Mathf.Max(.7f, Screen.width / 1400f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float w = Screen.width / scale, h = Screen.height / scale;
        var title = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(24, 16, w - 48, 38), "Hunt Happens · Hands Preview", title);
        GUI.Label(new Rect(26, 55, w - 52, 24), "Choose an animation below. Pause and drag the timeline to inspect finger bends.");
        float y = h - 146;
        GUI.Box(new Rect(16, y - 10, w - 32, 140), "");
        float bw = (w - 72) / 7;
        for (int i = 0; i < clips.Length; i++)
        {
            GUI.backgroundColor = i == selected ? new Color(.95f, .65f, .32f) : Color.white;
            if (GUI.Button(new Rect(28 + i * (bw + 2), y + 3, bw, 34), captions[i])) Select(i);
        }
        GUI.backgroundColor = Color.white;
        if (GUI.Button(new Rect(28, y + 48, 105, 30), paused ? "Play" : "Pause")) paused = !paused;
        float newTime = GUI.HorizontalSlider(new Rect(152, y + 60, w - 395, 20), time, 0, Mathf.Max(.01f, clips[selected].length));
        if (Mathf.Abs(newTime - time) > .0001f) { time = newTime; paused = true; }
        GUI.Label(new Rect(w - 220, y + 50, 190, 25), time.ToString("0.00") + " / " + clips[selected].length.ToString("0.00") + " sec");
        GUI.Label(new Rect(30, y + 93, 70, 24), "Speed");
        speed = GUI.HorizontalSlider(new Rect(90, y + 101, 160, 20), speed, .1f, 1.5f);
        GUI.Label(new Rect(265, y + 92, 70, 24), speed.ToString("0.0") + "x");
        if (GUI.Button(new Rect(w - 350, y + 88, 100, 28), "Front")) SetView(0);
        if (GUI.Button(new Rect(w - 240, y + 88, 100, 28), "Angle")) SetView(1);
        if (GUI.Button(new Rect(w - 130, y + 88, 100, 28), "Back")) SetView(2);
    }
}
