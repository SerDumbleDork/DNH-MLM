using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public int offsetTime = 0;

    // Update is called once per frame
    void Update()
    {

    }
    private void OnGUI()
    {
        float time = Time.realtimeSinceStartup + offsetTime;
        string timeText = "Time: "+((int)time / 60 / 60).ToString() + "h" +
                          ((int)time / 60 % 60).ToString() + "m " +
                          ((int)time % 60 % 60).ToString() + "s";
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 18;
        style.normal.textColor = Color.green;

        Rect boxRect = new Rect(930, 0, 250, 50);
        GUI.Box(boxRect, GUIContent.none);

        GUI.Label(new Rect(boxRect.x + 10,boxRect.y + 10,boxRect.width,boxRect.height),timeText,style);
    }
}
