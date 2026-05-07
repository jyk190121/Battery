using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PortableBattery : MonoBehaviour
{
    public static event System.Action OnBatteryUsed;

    private void Update()
    {
        if(gameObject.activeInHierarchy && Mouse.current.leftButton.wasPressedThisFrame)
        {
            UseBattery();
        }
    }

    void UseBattery()
    {
        OnBatteryUsed?.Invoke();
        Destroy(gameObject); // 배터리를 사용한 후 오브젝트 제거
    }
}
