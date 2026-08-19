using System;
using UnityEngine;

namespace SSP25DP1
{
    [Serializable]
    public class BoundaryPlayer
    {
        public float xMin, xMax, zMin, zMax;
    }

    public class PlayerController : MonoBehaviour
    {
        #region BOUNDARY VARIABLES
        [Header("BOUNDARY")]
        public BoundaryPlayer boundary;
        [SerializeField] float xMinOffset = 1.0f;
        [SerializeField] float xMaxOffset = 1.0f;
        [SerializeField] float zMinOffset = 1.0f;
        [SerializeField] float zMaxOffset = 1.0f;
        #endregion

        #region MOVEMENTS VARIABLES
        [Header("MOVEMENT")]
        [SerializeField] float speed;
        [SerializeField] float tilt;
        private Rigidbody rbPlayer;
        float moveHorizontal;
        float moveVertical;
        #endregion

        #region EXECUTION
        private void Awake()
        {
            rbPlayer = GetComponent<Rigidbody>();
        }

        void Start()
        {
            UpdateBoundary();
        }

        void Update()
        {
            moveHorizontal = Input.GetAxis("Horizontal");
            moveVertical = Input.GetAxis("Vertical");
        }

        private void FixedUpdate()
        {
            TranslationProcess();
        }
        #endregion

        #region BOUNDARY
        private void UpdateBoundary()
        {
            Vector2 half = CameraBoundary.GetHalfDimensionsInWorldUnits();
            boundary.xMin = -half.x + xMinOffset;
            boundary.xMax = half.x - xMaxOffset;
            boundary.zMin = -half.y + zMinOffset;
            boundary.zMax = half.y - zMaxOffset;
        }
        #endregion

        #region MOVEMENT
        // DIRECTIONAL
        private void TranslationProcess()
        {
            Vector3 movement = new Vector3(moveHorizontal, 0f, moveVertical);
            rbPlayer.linearVelocity = movement * speed;
            rbPlayer.position = new Vector3(Mathf.Clamp(rbPlayer.position.x, boundary.xMin, boundary.xMax), 0f, Mathf.Clamp(rbPlayer.position.z, boundary.zMin, boundary.zMax));
        }
        #endregion
    }
}