namespace LGH
{
    // Implement this on any script that holds state which must not leak from one room into the
    // next (doors left open, a safe someone unlocked, a clue already collected, ...).
    // The scene is never reloaded between rooms, so nothing resets on its own - RoomResetter
    // finds every IRoomResettable in the scene and calls this once a brand-new room is created.
    public interface IRoomResettable
    {
        void ResetForNewRoom();
    }
}
