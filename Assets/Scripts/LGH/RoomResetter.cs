using UnityEngine;

namespace LGH
{
    // Call ResetAll() once, right after a new room/session is created (host side, before anyone
    // joins). It finds every IRoomResettable script in the scene - doors, puzzles, collected
    // clues, anything added later - and puts it back to its fresh-room state.
    public static class RoomResetter
    {
        public static void ResetAll()
        {
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is IRoomResettable resettable) resettable.ResetForNewRoom();
            }
        }
    }
}
