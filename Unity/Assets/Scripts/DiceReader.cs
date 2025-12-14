using UnityEngine;

public class DiceReader : MonoBehaviour
{
    public Transform top;
    public Transform bottom;
    public Transform front;
    public Transform back;
    public Transform left;
    public Transform right;

    private Rigidbody rb;
    public bool isRolling = false;
    private int lastNumber = 1;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("❌ DiceReader: Rigidbody 없음!");
        }
    }

    public void RollDice()
    {
        if (isRolling)
        {
            Debug.LogWarning("⚠️ 이미 주사위가 굴러가는 중");
            return;
        }

        if (rb == null)
        {
            Debug.LogError("❌ Rigidbody 없음");
            return;
        }

        isRolling = true;
        Debug.Log("⏳ 주사위 굴러가는 중...");

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

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

        CancelInvoke(nameof(CheckStop));
        InvokeRepeating(nameof(CheckStop), 0.5f, 0.1f);
    }

    void CheckStop()
    {
        if (rb.velocity.magnitude < 0.05f && rb.angularVelocity.magnitude < 0.05f)
        {
            CancelInvoke(nameof(CheckStop));
            isRolling = false;

            int number = GetTopNumber();
            lastNumber = number;
            Debug.Log($"✅ 주사위 멈춤! 숫자: {number}");
        }
    }

    public int GetTopNumber()
    {
        if (isRolling)
        {
            Debug.LogWarning("⚠️ 주사위가 여전히 굴러가는 중!");
            return lastNumber;
        }

        Transform[] faces = { front, top, right, left, bottom, back };
        float maxDot = -999f;
        int result = -1;

        Vector3 up = Vector3.up;

        for (int i = 0; i < faces.Length; i++)
        {
            if (faces[i] == null)
            {
                Debug.LogError($"❌ faces[{i}] null!");
                continue;
            }

            Vector3 dir = faces[i].forward;
            float dot = Vector3.Dot(dir, up);

            Debug.Log($"📍 Face {i}: dot={dot:F2}");

            if (dot > maxDot)
            {
                maxDot = dot;
                result = i + 1;
            }
        }

        Debug.Log($"✅ 계산된 주사위 값: {result} (maxDot={maxDot:F2})");
        return result > 0 ? result : lastNumber;
    }
}
