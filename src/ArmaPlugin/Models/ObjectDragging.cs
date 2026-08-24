using System;
using System.Collections.Generic;
using System.Linq;

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
    private readonly object gate = new();
    private readonly Dictionary<string, ObjectDragSession> sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> generations = new(StringComparer.Ordinal);

    public ObjectDragStartResult TryStart(int ownerClientId, ObjectDragStart start)
    {
        ValidateIdentity(ownerClientId, start.ObjectId, start.DragId);
        if (start.Generation < 0 || start.Generation == long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(start.Generation));

        lock (gate)
        {
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

            int priority = string.CompareOrdinal(start.DragId, current.DragId);
            if (priority < 0 || priority == 0 && ownerClientId < current.OwnerClientId)
            {
                sessions[start.ObjectId] = new ObjectDragSession(start.ObjectId, start.DragId, ownerClientId, start.Generation);
                return ObjectDragStartResult.Replaced;
            }

            return ObjectDragStartResult.Rejected;
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
                || update.Sequence <= current.LastSequence)
                return false;

            current.LastSequence = update.Sequence;
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
            return sessions.Remove(end.ObjectId);
        }
    }

    public bool TryCancel(int ownerClientId, string objectId, string dragId)
    {
        ValidateIdentity(ownerClientId, objectId, dragId);

        lock (gate)
        {
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
                generations[session.ObjectId] = Math.Max(
                    generations.GetValueOrDefault(session.ObjectId),
                    checked(session.Generation + 1));
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
            sessions.Clear();
            generations.Clear();
            return released;
        }
    }

    private static void ValidateIdentity(int ownerClientId, string objectId, string dragId)
    {
        if (ownerClientId < 1) throw new ArgumentOutOfRangeException(nameof(ownerClientId));
        if (string.IsNullOrWhiteSpace(objectId)) throw new ArgumentException("Object ID cannot be empty.", nameof(objectId));
        if (string.IsNullOrWhiteSpace(dragId)) throw new ArgumentException("Drag ID cannot be empty.", nameof(dragId));
    }
}
