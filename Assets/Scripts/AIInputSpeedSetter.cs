using UnityEngine;
using UnityEngine.UI;

public class AIInputSpeedSetter : MonoBehaviour
{
    public PBTBridgeAIController controller;
    public InputField inputField;

    void Start()
    {
        if (inputField != null)
            inputField.text = controller.aiTimeScale.ToString("0.0");

        inputField.onEndEdit.AddListener(OnInputChanged);
    }

    void OnInputChanged(string value)
    {
        if (float.TryParse(value, out float speed))
        {
            speed = Mathf.Max(0.1f, speed);

            controller.aiTimeScale = speed;
            Time.timeScale = speed;
            Time.fixedDeltaTime = 0.02f / speed;

            Debug.Log("AI speed changed to: " + speed);
        }
        else
        {
            Debug.LogWarning("Invalid speed input: " + value);
        }
    }
}
