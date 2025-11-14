using UnityEngine;

public class DiceReader : MonoBehaviour
{
    public Transform top;       //2
    public Transform bottom;    //5
    public Transform front;     //1
    public Transform back;      //6
    public Transform left;      //4
    public Transform right;     //3

    private Rigidbody rb;
    private bool isRolling = false;
    public PlayerToken playerToken;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // 스페이스 누르면 주사위 굴림
        if (Input.GetKeyDown(KeyCode.Space))
        {
            RollDice();
        }
    }

    // ============================
    //      주사위를 굴리는 함수
    // ============================
    void RollDice()
    {
        if (isRolling) return;
        isRolling = true;

        // 속도 초기화
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 힘 + 회전
        rb.AddForce(new Vector3(
            Random.Range(-3f, 3f),
            Random.Range(7f, 10f),
            Random.Range(-3f, 3f)
        ), ForceMode.Impulse);

        rb.AddTorque(new Vector3(
            Random.Range(-20f, 20f),
            Random.Range(-20f, 20f),
            Random.Range(-20f, 20f)
        ), ForceMode.Impulse);

        // 멈췄는지 체크 시작
        InvokeRepeating(nameof(CheckStop), 0.5f, 0.1f);
    }

    void CheckStop()
    {
        if (rb.velocity.magnitude < 0.05f && rb.angularVelocity.magnitude < 0.05f)
        {
            CancelInvoke(nameof(CheckStop));
            isRolling = false;

            int number = GetTopNumber();
            Debug.Log("🎲 윗면 숫자 : " + number);
            if (playerToken != null) ;
        }
    }

    // ============================
    //      윗면 숫자 계산
    // ============================
    public int GetTopNumber()
    {
        Transform[] faces = { front, top, right, left, bottom, back };
        float maxDot = -999f;
        int result = -1;

        Vector3 up = Vector3.up;

        for (int i = 0; i < faces.Length; i++)
        {
            // 각 면의 Normal 방향(Z+)
            Vector3 dir = faces[i].transform.forward;

            float dot = Vector3.Dot(dir, up);

            if (dot > maxDot)
            {
                maxDot = dot;
                result = i + 1;
            }
        }

        return result;
    }
}