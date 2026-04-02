using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// 네임스페이스 충돌 방지
using Key = UnityEngine.InputSystem.Key;

public static class Input
{
    private static float sensitivity = 3f;
    private static float gravity = 3f;

    // 마우스 이동량(Delta)을 가져오는 함수
    //public static Vector2 GetMouseDelta() => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
    public static Vector2 GetMouseDelta()
    {
        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
    }

    private static Dictionary<string, float> axisValues = new Dictionary<string, float>
    {
        { "Horizontal", 0f },
        { "Vertical", 0f }
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        GameObject updaterObj = new GameObject("AxisUpdater");
        updaterObj.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(updaterObj);
        updaterObj.AddComponent<AxisUpdater>();
    }

    private class AxisUpdater : MonoBehaviour
    {
        private void Update()
        {
            UpdateAxis("Horizontal", GetAxisRaw("Horizontal"));
            UpdateAxis("Vertical", GetAxisRaw("Vertical"));
        }

        private void UpdateAxis(string name, float target)
        {
            float current = axisValues[name];
            if (target != 0)
                current = Mathf.MoveTowards(current, target, sensitivity * Time.deltaTime);
            else
                current = Mathf.MoveTowards(current, 0, gravity * Time.deltaTime);

            axisValues[name] = current;
        }
    }

    // --- 키보드 관련 함수들 ---

    public static bool GetKeyDown(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    public static bool GetKey(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;
    public static bool GetKeyUp(Key key) => Keyboard.current != null && Keyboard.current[key].wasReleasedThisFrame;

    // --- 마우스 관련 함수들 (0:왼쪽, 1:오른쪽, 2:휠클릭) ---

    public static bool GetMouseButtonDown(int button) => GetMouseState(button, "down");
    public static bool GetMouseButton(int button) => GetMouseState(button, "held");
    public static bool GetMouseButtonUp(int button) => GetMouseState(button, "up");

    private static bool GetMouseState(int button, string state)
    {
        if (Mouse.current == null) return false;

        var buttonControl = button switch
        {
            0 => Mouse.current.leftButton,
            1 => Mouse.current.rightButton,
            2 => Mouse.current.middleButton,
            _ => null
        };

        if (buttonControl == null) return false;

        return state switch
        {
            "down" => buttonControl.wasPressedThisFrame,
            "held" => buttonControl.isPressed,
            "up" => buttonControl.wasReleasedThisFrame,
            _ => false
        };
    }

    // --- 축(Axis) 관련 함수들 ---

    public static float GetAxis(string axisName) => axisValues.ContainsKey(axisName) ? axisValues[axisName] : 0f;

    public static float GetAxisRaw(string axisName)
    {
        if (Keyboard.current == null) return 0f;
        float val = 0f;
        if (axisName == "Horizontal")
        {
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) val += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) val -= 1f;
        }
        else if (axisName == "Vertical")
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) val += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) val -= 1f;
        }
        return val;
    }
}