using UnityEngine;

public class NukeState : ActionState
{
    public override void OnStateStart()
    {
        var entries = RollStateManager.Instance.NukeEntries;
        foreach (var entry in entries)
        {
            ResolveNuke(entry);
        }

        CompleteState();
    }

    private void ResolveNuke(RollStateManager.NukeEntry entry)
    {
        // TODO: implement nuke resolution (VFX, damage, etc.)
        Debug.Log($"[NukeState] Resolve nuke '{entry.Nuke.actionName}' x{entry.Count}");
    }
}

