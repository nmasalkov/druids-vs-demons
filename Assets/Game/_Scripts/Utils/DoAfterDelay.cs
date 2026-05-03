using System;
using System.Collections;
using UnityEngine;

namespace Utils
{
    public class DoAfterDelay
    {
        public static void Execute(Action action, float delay)
        {
            var runner = GetOrCreateCoroutineRunner();
            runner.StartCoroutine(DelayedActionCoroutine(action, delay));
        }

        private static IEnumerator DelayedActionCoroutine(Action action, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            action?.Invoke();
        }

        private static CoroutineRunner GetOrCreateCoroutineRunner()
        {
            var runner = GameObject.FindObjectOfType<CoroutineRunner>();
            if (runner == null)
            {
                var go = new GameObject("CoroutineRunner");
                runner = go.AddComponent<CoroutineRunner>();
            }
            return runner;
        }
    }

    public class CoroutineRunner : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}