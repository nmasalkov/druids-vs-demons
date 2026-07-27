using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Splines;
using Utils;

/// <summary>
/// One encounter's point on the map: toggles its Future/Current/Complete visuals, optionally reveals
/// the spline paths leading in/out of it, and plays a short "arrived" feedback when it becomes the
/// current encounter (a shake for a fight, a yoyo/punch scale otherwise). Which EncounterSO this
/// point represents is assigned once by MapManager.AssignEncounters() (positional against
/// EncounterListSO), not wired per-instance in the Inspector. See MapManager and docs/Encounters.md.
/// </summary>
public class MapEncounterPoint : MonoBehaviour
{
    // Serialized (not just an auto-property) so it's visible in the Inspector even though it's
    // runtime-assigned — CLAUDE.md rule 19: load-bearing runtime state stays visible by default,
    // not buried in a private field only a debugger can see.
    [SerializeField] private EncounterSO encounterSO;
    public EncounterSO EncounterSO => encounterSO;

    [SerializeField] private GameObject futureEncounterVisual;
    [SerializeField] private GameObject currentEncounterVisual;
    [SerializeField] private GameObject completeEncounterVisual;

    [Tooltip("Optional. The spline path the player walks in on to reach this point.")]
    [SerializeField] private SplineContainer pathIn;
    [Tooltip("Optional. The spline path the player walks out on after this point.")]
    [SerializeField] private SplineContainer pathOut;

    [Tooltip("Played when this point becomes the current encounter and it's a fight.")]
    [FormerlySerializedAs("reachFeedback")]
    [SerializeField] private MMF_Player arriveFightFeedback;
    [Tooltip("Played when this point becomes the current encounter and it's not a fight (a yoyo/punch, as opposed to the fight's shake).")]
    [FormerlySerializedAs("arriveFeedback")]
    [SerializeField] private MMF_Player arriveRegularFeedback;

    public void SetEncounter(EncounterSO encounter) => encounterSO = encounter;

    public void SetFuture()
    {
        futureEncounterVisual.SetActive(true);
        currentEncounterVisual.SetActive(false);
        completeEncounterVisual.SetActive(false);
    }

    public void SetCurrent()
    {
        futureEncounterVisual.SetActive(false);
        currentEncounterVisual.SetActive(true);
        completeEncounterVisual.SetActive(false);
    }

    public void SetComplete()
    {
        futureEncounterVisual.SetActive(false);
        currentEncounterVisual.SetActive(false);
        completeEncounterVisual.SetActive(true);
    }

    public void HidePaths()
    {
        if (pathIn != null) pathIn.gameObject.SetActive(false);
        if (pathOut != null) pathOut.gameObject.SetActive(false);
    }

    public void ShowPathIn()
    {
        if (pathIn != null) pathIn.gameObject.SetActive(true);
    }

    public void ShowPathOut()
    {
        if (pathOut != null) pathOut.gameObject.SetActive(true);
    }

    /// <summary>
    /// Reveals pathOut one dot at a time across duration seconds, instead of all at once — imitates
    /// the hero steadily walking it. Relies on a SplineInstantiate component on pathOut having already
    /// placed dot instances along the spline (SplineInstantiate parents its instances one level deep,
    /// under its own runtime-created "instances root" child — SetActive(true) triggers its OnEnable()
    /// synchronously, so the dots already exist by the time this reads them). No-op if pathOut is
    /// unassigned or has no dot instances (e.g. no SplineInstantiate on it) — falls back to nothing
    /// being shown rather than throwing, since paths are optional (see docs/Encounters.md).
    /// </summary>
    public void RevealPathOutGradually(float duration)
    {
        if (pathOut == null) return;

        pathOut.gameObject.SetActive(true);
        var dots = FindInstantiatedDots(pathOut.transform);
        if (dots.Count == 0) return;

        float perDotDelay = duration / dots.Count;
        for (int i = 0; i < dots.Count; i++)
        {
            var dot = dots[i];
            dot.SetActive(false);
            DoAfterDelay.Execute(() => dot.SetActive(true), i * perDotDelay);
        }
    }

    private static List<GameObject> FindInstantiatedDots(Transform splineTransform)
    {
        var dots = new List<GameObject>();
        if (splineTransform.childCount == 0) return dots;

        var instancesRoot = splineTransform.GetChild(0);
        foreach (Transform dot in instancesRoot)
            dots.Add(dot.gameObject);
        return dots;
    }

    public void PlayArriveFightFeedback() => arriveFightFeedback.PlayFeedbacks();
    public void PlayArriveRegularFeedback() => arriveRegularFeedback.PlayFeedbacks();
}
