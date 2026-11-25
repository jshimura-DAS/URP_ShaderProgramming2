using UnityEngine;
using UnityEngine.InputSystem;

public class WipeController : MonoBehaviour
{
    [Tooltip("進行度が変化する速さ（1 秒あたり）")]
    public float speed = 1.5f;

    [Tooltip("true なら覆う方向、false なら戻す方向へ動く")]
    public bool covering = true;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            covering = !covering;
        }

        float target = covering ? 1f : 0f;
        WipeRendererFeature.Progress = Mathf.MoveTowards(
            WipeRendererFeature.Progress, target, speed * Time.deltaTime);
    }
}