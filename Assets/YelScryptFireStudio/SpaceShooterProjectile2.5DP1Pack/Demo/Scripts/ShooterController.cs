using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSP25DP1
{
    public class ShooterController : MonoBehaviour
    {
        #region SHOOTING VARIABLES
        [Header("SHOOTING")]
        [SerializeField] float fireRate;
        [SerializeField] Transform flarePoint;
        [SerializeField] Transform shotPoint;
        float nextFire;
        int currentShot = 0;

        [Serializable]
        public class Shooting
        {
            public GameObject flare;
            public GameObject shot;
        }
        [SerializeField] List<Shooting> shootingList = new List<Shooting>();
        #endregion

        #region EXECUTION
        private void Update()
        {
            if ((Input.GetButton("Fire1") || Input.GetKey(KeyCode.Space)) && Time.time > nextFire)
            {
                ShootingProcess();
            }

            if (Input.GetKeyDown(KeyCode.E) && (currentShot >= 0 && currentShot < shootingList.Count - 1))
            {
                SelectShot(1);
            }

            if (Input.GetKeyDown(KeyCode.Q) && (currentShot > 0 && currentShot <= shootingList.Count - 1))
            {
                SelectShot(-1);
            }
        }
        #endregion

        #region SHOOTING
        private void SelectShot(int valueDirection)
        {
            currentShot = currentShot + valueDirection % (shootingList.Count - 1);
        }

        private void ShootingProcess()
        {
            nextFire = Time.time + fireRate;
            GameObject fxFlare = Instantiate(shootingList[currentShot].flare, flarePoint.position, flarePoint.rotation);
            fxFlare.transform.parent = gameObject.transform;
            Instantiate(shootingList[currentShot].shot, shotPoint.position, shotPoint.rotation);
        }
        #endregion
    }
}