using UnityEngine;

namespace ArenaSurvival
{
    /// <summary>Камера вида сверху под углом: плавно следует за игроком, угол наклона фиксирован.</summary>
    public class TopDownCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Range(30f, 85f)] private float pitch = 55f;
        [SerializeField, Min(3f)] private float distance = 16f;
        [SerializeField, Min(0f)] private float followSharpness = 8f;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

        private void Start()
        {
            if (target == null) return;
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.position = DesiredPosition();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            float k = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, DesiredPosition(), k);
        }

        private Vector3 DesiredPosition()
        {
            float rad = pitch * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(0f, Mathf.Sin(rad), -Mathf.Cos(rad)) * distance;
            return target.position + targetOffset + offset;
        }
    }
}
