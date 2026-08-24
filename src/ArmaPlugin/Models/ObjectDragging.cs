using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EdenOnline;

public sealed class ObjectDragStart
{
    public string ObjectId { get; set; } = "";
    public string DragId { get; set; } = "";
    public long Generation { get; set; }

    public ObjectDragStart() { }

    public ObjectDragStart(string objectId, string dragId)
    {
        ObjectId = objectId;
        DragId = dragId;
    }
}

public sealed class ObjectDragUpdate
{
    public string ObjectId { get; set; } = "";
    public string DragId { get; set; } = "";
    public long Sequence { get; set; }
    public object[] Position { get; set; } = [];
    public object[] Rotation { get; set; } = [];

    public ObjectDragUpdate() { }

    public ObjectDragUpdate(
        string objectId,
        string dragId,
        long sequence,
        object[] position,
        object[] rotation)
    {
        ObjectId = objectId;
        DragId = dragId;
        Sequence = sequence;
        Position = position;
        Rotation = rotation;
    }
}

public sealed class ObjectDragStartAcknowledgement
{
    public string ObjectId { get; set; } = "";
    public string DragId { get; set; } = "";
    public bool Accepted { get; set; }

    public ObjectDragStartAcknowledgement() { }

    public ObjectDragStartAcknowledgement(string objectId, string dragId, bool accepted)
    {
        ObjectId = objectId;
        DragId = dragId;
        Accepted = accepted;
    }
}

public sealed class ObjectDragEnd
{
    public string ObjectId { get; set; } = "";
    public string DragId { get; set; } = "";
    public long FinalSequence { get; set; }
    public object[] Position { get; set; } = [];
    public object[] Rotation { get; set; } = [];
    public long Generation { get; set; }
    public long NextGeneration { get; set; }

    public ObjectDragEnd() { }

    public ObjectDragEnd(
        string objectId,
        string dragId,
        long finalSequence,
        object[] position,
        object[] rotation,
        long generation = 0,
        long? nextGeneration = null)
    {
        ObjectId = objectId;
        DragId = dragId;
        FinalSequence = finalSequence;
        Position = position;
        Rotation = rotation;
        Generation = generation;
        NextGeneration = nextGeneration ?? checked(generation + 1);
    }
}

public enum ObjectDragStartResult
{
    Accepted,
    Replaced,
    Duplicate,
    Rejected
}

public sealed class ObjectDragSession
{
    public string ObjectId { get; }
    public string DragId { get; }
    public int OwnerClientId { get; }
    public long Generation { get; }
    public long LastSequence { get; internal set; }
    public bool IsEnding { get; internal set; }

    internal ObjectDragSession(string objectId, string dragId, int ownerClientId, long generation)
    {
        ObjectId = objectId;
        DragId = dragId;
        OwnerClientId = ownerClientId;
        Generation = generation;
    }
}

/// <summary>
/// Orders peer-to-peer drag messages before they are forwarded into Arma.
/// </summary>
public sealed class ObjectDragSessionManager
{
    private sealed class Acquisition(IEnumerable<int> expectedPeerIds)
    {
        public HashSet<int> ExpectedPeerIds { get; } = [.. expectedPeerIds];
        public HashSet<int> AcceptedPeerIds { get; } = [];
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private const int ClosedDragLimit = 4096;
    private readonly object gate = new();
    private readonly Dictionary<string, ObjectDragSession> sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> generations = new(StringComparer.Ordinal);
    private readonly Dictionary<(string ObjectId, string DragId), Acquisition> acquisitions = [];
    private readonly HashSet<string> closedDragIds = new(StringComparer.Ordinal);
    private readonly Queue<string> closedDragOrder = new();

    public ObjectDragStartResult TryStart(int ownerClientId, ObjectDragStart start)
    {
        ValidateIdentity(ownerClientId, start.ObjectId, start.DragId);
        if (start.Generation < 0 || start.Generation == long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(start.Generation));

        lock (gate)
        {
            if (closedDragIds.Contains(start.DragId)) return ObjectDragStartResult.Rejected;

            long knownGeneration = generations.GetValueOrDefault(start.ObjectId);
            if (start.Generation < knownGeneration) return ObjectDragStartResult.Rejected;

            if (!sessions.TryGetValue(start.ObjectId, out ObjectDragSession? current))
            {
                generations[start.ObjectId] = start.Generation;
                sessions[start.ObjectId] = new ObjectDragSession(start.ObjectId, start.DragId, ownerClientId, start.Generation);
                return ObjectDragStartResult.Accepted;
            }

            if (current.OwnerClientId == ownerClientId && current.DragId == start.DragId)
                return ObjectDragStartResult.Duplicate;

            if (start.Generation < current.Generation) return ObjectDragStartResult.Rejected;
            if (start.Generation > current.Generation)
            {
                generations[start.ObjectId] = start.Generation;
                sessions[start.ObjectId] = new ObjectDragSession(start.ObjectId, start.DragId, ownerClientId, start.Generation);
                return ObjectDragStartResult.Replaced;
            }

            return ObjectDragStartResult.Rejected;
        }
    }

    public ObjectDragStartResult TryBeginAcquisition(
        int ownerClientId,
        ObjectDragStart start,
        IEnumerable<int> expectedPeerIds)
    {
        ObjectDragStartResult result = TryStart(ownerClientId, start);
        if (result is not ObjectDragStartResult.Accepted) return result;

        lock (gate)
        {
            var acquisition = new Acquisition(expectedPeerIds.Where(peerId => peerId != ownerClientId));
            acquisitions[(start.ObjectId, start.DragId)] = acquisition;
            if (acquisition.ExpectedPeerIds.Count == 0)
                acquisition.Completion.TrySetResult(true);
        }

        return result;
    }

    public void AcknowledgeAcquisition(int peerClientId, ObjectDragStartAcknowledgement acknowledgement)
    {
        if (peerClientId < 1) throw new ArgumentOutOfRangeException(nameof(peerClientId));
        if (string.IsNullOrWhiteSpace(acknowledgement.ObjectId))
            throw new ArgumentException("Object ID cannot be empty.", nameof(acknowledgement));
        if (string.IsNullOrWhiteSpace(acknowledgement.DragId))
            throw new ArgumentException("Drag ID cannot be empty.", nameof(acknowledgement));

        lock (gate)
        {
            if (!acquisitions.TryGetValue((acknowledgement.ObjectId, acknowledgement.DragId), out Acquisition? acquisition))
                return;

            if (!acknowledgement.Accepted)
            {
                acquisition.Completion.TrySetResult(false);
                return;
            }

            if (acquisition.ExpectedPeerIds.Contains(peerClientId))
                acquisition.AcceptedPeerIds.Add(peerClientId);

            if (acquisition.AcceptedPeerIds.IsSupersetOf(acquisition.ExpectedPeerIds))
                acquisition.Completion.TrySetResult(true);
        }
    }

    public async Task<bool> WaitForAcquisitionAsync(
        string objectId,
        string dragId,
        TimeSpan timeout)
    {
        Task<bool> completion;
        lock (gate)
        {
            if (!acquisitions.TryGetValue((objectId, dragId), out Acquisition? acquisition)) return false;
            completion = acquisition.Completion.Task;
        }

        try
        {
            return await completion.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            lock (gate)
            {
                acquisitions.Remove((objectId, dragId));
            }
        }
    }

    public bool TryGetActive(string objectId, out ObjectDragSession? session)
    {
        if (string.IsNullOrWhiteSpace(objectId)) throw new ArgumentException("Object ID cannot be empty.", nameof(objectId));

        lock (gate)
        {
            return sessions.TryGetValue(objectId, out session);
        }
    }

    public long GetGeneration(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId)) throw new ArgumentException("Object ID cannot be empty.", nameof(objectId));

        lock (gate)
        {
            return generations.GetValueOrDefault(objectId);
        }
    }

    /// <summary>
    /// Records the persisted object revision received through normal object
    /// synchronization so delayed drag messages cannot reopen an older state.
    /// </summary>
    public void ObserveGeneration(string objectId, double generation)
    {
        if (string.IsNullOrWhiteSpace(objectId)) throw new ArgumentException("Object ID cannot be empty.", nameof(objectId));
        if (!double.IsFinite(generation)
            || generation < 0
            || generation > long.MaxValue
            || generation != Math.Truncate(generation))
            throw new ArgumentOutOfRangeException(nameof(generation));

        lock (gate)
        {
            long observedGeneration = (long)generation;
            generations[objectId] = Math.Max(generations.GetValueOrDefault(objectId), observedGeneration);
        }
    }

    public bool TryAdvance(int ownerClientId, ObjectDragUpdate update)
    {
        ValidateIdentity(ownerClientId, update.ObjectId, update.DragId);
        if (update.Sequence < 1) return false;

        lock (gate)
        {
            if (!sessions.TryGetValue(update.ObjectId, out ObjectDragSession? current)
                || current.OwnerClientId != ownerClientId
                || current.DragId != update.DragId
                || current.IsEnding
                || update.Sequence <= current.LastSequence)
                return false;

            current.LastSequence = update.Sequence;
            return true;
        }
    }

    public bool TryPrepareEnd(int ownerClientId, ObjectDragEnd end)
    {
        ValidateIdentity(ownerClientId, end.ObjectId, end.DragId);
        if (end.FinalSequence < 0) return false;

        lock (gate)
        {
            if (!sessions.TryGetValue(end.ObjectId, out ObjectDragSession? current)
                || current.OwnerClientId != ownerClientId
                || current.DragId != end.DragId
                || current.Generation != end.Generation
                || end.NextGeneration <= current.Generation
                || end.FinalSequence < current.LastSequence)
                return false;

            current.IsEnding = true;
            return true;
        }
    }

    public bool TryEnd(int ownerClientId, ObjectDragEnd end)
    {
        ValidateIdentity(ownerClientId, end.ObjectId, end.DragId);
        if (end.FinalSequence < 0) return false;

        lock (gate)
        {
            if (!sessions.TryGetValue(end.ObjectId, out ObjectDragSession? current)
                || current.OwnerClientId != ownerClientId
                || current.DragId != end.DragId
                || current.Generation != end.Generation
                || end.NextGeneration <= current.Generation
                || end.FinalSequence < current.LastSequence)
                return false;

            generations[end.ObjectId] = Math.Max(generations.GetValueOrDefault(end.ObjectId), end.NextGeneration);
            RememberClosedDrag(end.DragId);
            acquisitions.Remove((end.ObjectId, end.DragId));
            return sessions.Remove(end.ObjectId);
        }
    }

    public bool TryCancel(int ownerClientId, string objectId, string dragId)
    {
        ValidateIdentity(ownerClientId, objectId, dragId);

        lock (gate)
        {
            RememberClosedDrag(dragId);
            acquisitions.Remove((objectId, dragId));
            if (!sessions.TryGetValue(objectId, out ObjectDragSession? current)
                || current.OwnerClientId != ownerClientId
                || current.DragId != dragId)
                return false;

            return sessions.Remove(objectId);
        }
    }

    public IReadOnlyList<ObjectDragSession> ReleaseOwner(int ownerClientId)
    {
        if (ownerClientId < 1) throw new ArgumentOutOfRangeException(nameof(ownerClientId));

        lock (gate)
        {
            List<ObjectDragSession> released = sessions.Values
                .Where(session => session.OwnerClientId == ownerClientId)
                .ToList();
            foreach (ObjectDragSession session in released)
            {
                RememberClosedDrag(session.DragId);
                acquisitions.Remove((session.ObjectId, session.DragId));
                sessions.Remove(session.ObjectId);
            }

            return released;
        }
    }

    public IReadOnlyList<ObjectDragSession> Clear()
    {
        lock (gate)
        {
            List<ObjectDragSession> released = [.. sessions.Values];
            foreach (Acquisition acquisition in acquisitions.Values)
                acquisition.Completion.TrySetResult(false);
            sessions.Clear();
            generations.Clear();
            acquisitions.Clear();
            closedDragIds.Clear();
            closedDragOrder.Clear();
            return released;
        }
    }

    private static void ValidateIdentity(int ownerClientId, string objectId, string dragId)
    {
        if (ownerClientId < 1) throw new ArgumentOutOfRangeException(nameof(ownerClientId));
        if (string.IsNullOrWhiteSpace(objectId)) throw new ArgumentException("Object ID cannot be empty.", nameof(objectId));
        if (string.IsNullOrWhiteSpace(dragId)) throw new ArgumentException("Drag ID cannot be empty.", nameof(dragId));
    }

    private void RememberClosedDrag(string dragId)
    {
        if (!closedDragIds.Add(dragId)) return;

        closedDragOrder.Enqueue(dragId);
        while (closedDragOrder.Count > ClosedDragLimit)
            closedDragIds.Remove(closedDragOrder.Dequeue());
    }
}
