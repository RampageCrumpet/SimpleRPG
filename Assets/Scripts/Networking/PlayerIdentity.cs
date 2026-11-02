using System;
using UnityEngine;

namespace SimpleRPG.Networking
{
    /// <summary>
    /// Who this player is, as a connecting client says it. The same id on every connection is how a player who drops out finds their
    /// character waiting when they come back.
    /// <para>
    /// This is a stand-in. An id the client makes up proves nothing: whoever learns it can take that player's place. Official servers must
    /// replace it with an identity from the login service and have the server check it; the rest of the game only ever sees the string.
    /// </para>
    /// For two clients on one machine, which would otherwise share an id, start one with <c>-playerId some-other-id</c>.
    /// </summary>
    public static class PlayerIdentity
    {
        private const string PrefsKey = "SimpleRPG.PlayerId";
        private const string CommandLineFlag = "-playerId";

        private static string current;

        /// <summary>This client's id: given on the command line, or else made once and kept.</summary>
        public static string Current
        {
            get
            {
                if (current == null)
                {
                    current = FromCommandLine() ?? FromPrefs();
                }

                return current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => current = null;

        private static string FromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == CommandLineFlag && SessionHandshake.IsValidPlayerId(args[i + 1]))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static string FromPrefs()
        {
            string stored = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (SessionHandshake.IsValidPlayerId(stored))
            {
                return stored;
            }

            string made = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PrefsKey, made);
            PlayerPrefs.Save();
            return made;
        }
    }
}
