using UnityEngine;
using UnityEngine.InputSystem;
using Key = UnityEngine.InputSystem.Key;


public class Player : MonoBehaviour
{

    public GameObject playerObject;
    public float moveSpeed = 5f;

    Animator anim;

    void Start()
    {
        playerObject = GetComponent<GameObject>();
        anim = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current == null) return;

        // 1. 현재 입력 축 값을 기반으로 실제 입력 강도(Magnitude)를 계산합니다.
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // 입력 벡터의 길이를 구합니다. (0 ~ 1 사이의 값)
        float inputMagnitude = new Vector2(h, v).magnitude;

        // 2. 고정된 moveSpeed 대신, 현재 입력 강도를 애니메이터에 전달합니다.
        // 이렇게 하면 키를 뗄 때 inputMagnitude가 0이 되어 Idle로 돌아갑니다.
        anim.SetFloat("Speed", inputMagnitude);

        Move(h, v);
    }

    void Move(float h, float v)
    {
        Vector3 moveDir = new Vector3(h, 0, v);

        if (moveDir.sqrMagnitude > 1f)
        {
            moveDir.Normalize();
        }

        //transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.World);
        //transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.Self);

        Vector3 worldMoveDir = transform.TransformDirection(moveDir);
        transform.position += worldMoveDir * moveSpeed * Time.deltaTime;
    }
}
