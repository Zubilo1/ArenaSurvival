using UnityEngine;

namespace ArenaSurvival
{
    /// <summary>
    /// Движение игрока (WASD, Shift — бег) через CharacterController и поворот к курсору мыши
    /// на игровой плоскости. CharacterController не даёт пройти сквозь стены и пол.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Движение")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 3.5f;
        [SerializeField, Min(0.1f)] private float runSpeed = 6f;
        [Tooltip("Если выключить, персонаж всегда ходит и не бегает")]
        [SerializeField] private bool allowRun = true;
        [SerializeField, Min(1f)] private float acceleration = 25f;
        [SerializeField] private float gravity = -20f;

        [Header("Поворот к мыши")]
        [SerializeField, Min(30f)] private float rotationSpeed = 720f;
        [SerializeField] private Camera aimCamera;

        [Header("Анимация")]
        [SerializeField] private ProceduralRig rig;

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (aimCamera == null) aimCamera = Camera.main;
            if (rig == null) rig = GetComponentInChildren<ProceduralRig>();
        }

        private void Update()
        {
            if (!GameManager.IsPlaying)
            {
                planarVelocity = Vector3.zero;
                if (rig != null) rig.SetSpeed(0f);
                return;
            }

            Vector2 input = GameInput.Move;
            float maxSpeed = (allowRun && GameInput.RunHeld) ? runSpeed : walkSpeed;
            Vector3 targetVelocity = new Vector3(input.x, 0f, input.y) * maxSpeed;
            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, acceleration * Time.deltaTime);

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;

            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

            RotateToMouse();
            if (rig != null) rig.SetSpeed(planarVelocity.magnitude);
        }

        private void RotateToMouse()
        {
            if (aimCamera == null) return;

            Ray ray = aimCamera.ScreenPointToRay(GameInput.PointerPosition);
            Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (!plane.Raycast(ray, out float enter)) return;

            Vector3 direction = ray.GetPoint(enter) - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.04f) return;

            Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotationSpeed * Time.deltaTime);
        }
    }
}
