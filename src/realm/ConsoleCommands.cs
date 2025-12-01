/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2025 WAAD Team <https://arbonne.games-rpg.net/>
 *
 * From original Ascent MMORPG Server, 2005-2008, which doesn't exist anymore.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *
 */

using System;
using WaadShared.Network;
using WaadShared.Database;
using WaadShared.Auth;
using WaadShared;
using static WaadShared.ConsoleCommands;
using static WaadShared.Common;
using static WaadShared.Opcodes;
using static WaadRealmServer.Master;

namespace WaadRealmServer
{
    public interface IConsole
    {
        void Write(string format, params object[] args);
    }

    public static class ConsoleCommands
    {
        // Ban un compte (HandleBanAccountCommand)
        public static bool HandleBanAccountCommand(IConsole console, int argc, string[] argv)
        {
            if (argc < 3)
                return false;
            // Appliquer le ban dans la base de données
            // argv[1] = compte, argv[2] = durée (ex: 3600 pour 1h, 0 = permanent)
            string account = argv[1];
            string period = argv[2];
            _ = int.TryParse(period, out int timeperiod);
            // Calcul du timestamp de fin de ban
            long bannedUntil = timeperiod > 0 ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() + timeperiod : 1;
            // Échappement du nom d'utilisateur
            var db = RealmDatabaseManager.GetDatabase();
            bool ok = false;
            if (db != null)
            {
                string safeAccount = db.EscapeString(account);
                ok = db.Execute("UPDATE accounts SET banned = {0} WHERE login = '{1}'", bannedUntil, safeAccount);
            }
            console.Write(R_N_CONCMD_A, account, ok ? R_N_CONCMD_A_1 : R_N_CONCMD_A_2, bannedUntil > 1 ? DateTimeOffset.FromUnixTimeSeconds(bannedUntil).ToString() : "");
            return true;
        }

        // Unban un compte (HandleUnbanAccountCommand)
        public static bool HandleUnbanAccountCommand(IConsole console, int argc, string[] argv)
        {
            if (argc < 2)
                return false;
            // Appliquer l'unban dans la base de données
            string account = argv[1];
            var db = RealmDatabaseManager.GetDatabase();
            bool ok = false;
            if (db != null)
            {
                string safeAccount = db.EscapeString(account);
                ok = db.Execute("UPDATE accounts SET banned = 0 WHERE login = '{0}'", safeAccount);
            }
            console.Write(ok ? R_N_CONCMD_A_3 : R_N_CONCMD_A_6, account);
            return true;
        }

        // Backup DB (HandleBackupDBCommand)
        public static bool HandleBackupDBCommand(IConsole console, int argc, string[] argv)
        {
            // Sauvegarde de la base selon le backend
            var db = RealmDatabaseManager.GetDatabase();
            bool ok;
            string backend = db?.GetType().Name ?? "aucun";
            string backupFile = "backup.sql";
            try
            {
                if (db is MySQLDatabase mysql)
                {
                    ok = mysql.DumpDatabase(backupFile);
                }
                else if (db is PostgresDatabase pg)
                {
                    ok = pg.DumpDatabase(backupFile);
                }
                else if (db is SQLiteDatabase sqlite)
                {
                    backupFile = "backup.sqlite";
                    ok = sqlite.DumpDatabase(backupFile);
                }
                else
                {
                    ok = false;
                }
            }
            catch (Exception ex)
            {
                CLog.Error("Erreur lors de la sauvegarde : {0}", ex.Message);
                ok = false;
            }
            if (ok)
                CLog.Success("[Command]", $"Backup effectué avec succès dans {backupFile}.");
            else
                console.Write($"Backup non supporté ou erreur sur ce backend : {backend}.");
            return true;
        }

        // Annule le shutdown (HandleCancelCommand)
        public static bool HandleCancelCommand(IConsole console, int argc, string[] argv)
        {
            StopEvent = false;
            console.Write(R_N_CONCMD_S_1);
            return true;
        }

        // Crée un compte (HandleCreateAccountCommand)
        public static bool HandleCreateAccountCommand(IConsole console, int argc, string[] argv)
        {
            if (argc < 6)
                return false;
            // argv[1] = login, argv[2] = password, argv[3] = email, argv[4] = gm, argv[5] = flags
            string login = argv[1];
            string password = argv[2];
            string email = argv[3];
            string gm = argv[4];
            string flags = argv[5];
            var db = RealmDatabaseManager.GetDatabase();
            bool result = false;
            if (db != null)
            {
                string safeLogin = db.EscapeString(login);
                string safePassword = db.EscapeString(password);
                string safeEmail = db.EscapeString(email);
                string safeGM = db.EscapeString(gm);
                string safeFlags = db.EscapeString(flags);
                // Création du compte principal
                result = db.Execute(
                    "INSERT INTO accounts (login, encrypted_password, email, gm, flags) VALUES ('{0}', '{1}', '{2}', '{3}', {4})",
                    safeLogin, safePassword, safeEmail, safeGM, safeFlags);
                if (result)
                {
                    // Récupérer l'ID du compte nouvellement créé
                    var acctResult = db.Query("SELECT acct FROM accounts WHERE login = '{0}'", safeLogin);
                    if (acctResult != null && acctResult.NextRow())
                    {
                        int acctId = Convert.ToInt32(acctResult.GetValue(0));
                        // Générer salt et verifier SRP6
                        var srp = new WowSRP6();
                        var salt = WowSRP6.ComputeSalt();
                        var verifier = srp.ComputeVerifier(safeLogin, safePassword, salt);
                        // Insérer dans account_data (en hex)
                        db.Execute("INSERT INTO account_data (acct, salt, verifier) VALUES ({0}, '{1}', '{2}')", acctId, salt.ToString(), verifier.ToString());
                    }
                }
            }
            console.Write(result ? R_N_CONCMD_A_4 : R_N_CONCMD_A_5);
            return true;
        }

        // Reset password (HandleResetPasswordCommand)
        public static bool HandleResetPasswordCommand(IConsole console, int argc, string[] argv)
        {
            if (argc < 4)
                return false;
            if (argv[2] != argv[3])
            {
                console.Write(R_N_CONCMD_PW);
                return true;
            }
            // argv[1] = compte, argv[2] = nouveau mdp, argv[3] = confirmation
            string account = argv[1];
            string newPassword = argv[2];
            var db = RealmDatabaseManager.GetDatabase();
            bool result = false;
            if (db != null)
            {
                string safeAccount = db.EscapeString(account);
                string safePassword = db.EscapeString(newPassword);
                // Met à jour le mot de passe chiffré dans la table accounts
                result = db.Execute("UPDATE accounts SET encrypted_password = '{0}' WHERE login = '{1}'", safePassword, safeAccount);
                if (result)
                {
                    // Récupérer l'ID du compte
                    var acctResult = db.Query("SELECT acct FROM accounts WHERE login = '{0}'", safeAccount);
                    if (acctResult != null && acctResult.NextRow())
                    {
                        int acctId = Convert.ToInt32(acctResult.GetValue(0));
                        // Générer un nouveau salt et verifier SRP6
                        var srp = new WowSRP6();
                        var salt = WowSRP6.ComputeSalt();
                        var verifier = srp.ComputeVerifier(safeAccount, safePassword, salt);
                        // Met à jour la table account_data
                        db.Execute("UPDATE account_data SET salt = '{0}', verifier = '{1}' WHERE acct = {2}", salt.ToString(), verifier.ToString(), acctId);
                    }
                }
            }
            console.Write(result ? R_N_CONCMD_PW_1 : R_N_CONCMD_A_5);
            return true;
        }

        // Rehash (HandleRehashCommand)
        public static bool HandleRehashCommand(IConsole console, int argc, string[] argv)
        {
            Rehash(true);
            console.Write(R_N_CONCMD_RC);
            return true;
        }

        // Save all (HandleSaveAllCommand)
        public static bool HandleSaveAllCommand()
        {
            // Sauvegarde tous les joueurs connectés
            var players = ClientMgr.Instance.GetAllPlayers();
            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int count = 0;
            foreach (var player in players)
            {
                try
                {
                    var session = ClientMgr.Instance.GetSessionByRPInfo(player);
                    if (session != null)
                    {
                        session.Update();
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    CLog.Error("[Command]", "Erreur lors de la sauvegarde du joueur {0} : {1}", player.Name, ex.Message);
                }
            }
            var elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime;

            CLog.Success("[Command]", "Sauvegarde de {0} joueurs en ligne en {1} ms.", count.ToString(), elapsed.ToString());
            return true;
        }

        // Shutdown (HandleShutDownCommand)
        public static bool HandleShutDownCommand(IConsole console, int argc, string[] argv)
        {
            int delay = 5;
            if (argc >= 2 && int.TryParse(argv[1], out var d))
                delay = d;
            console.Write($"Arrêt du serveur dans {delay} seconde(s)...");
            System.Threading.Thread.Sleep(delay * 1000);
            ServerShutdown = true;
            console.Write(R_N_CONCMD_S);
            return true;
        }

        // Whisper (HandleWhisperCommand)
        public static bool HandleWhisperCommand(IConsole console, int argc, string[] argv)
        {
            if (argc < 3)
                return false;

            var player = ClientMgr.Instance.GetRPlayer(argv[1]);
            if (player == null || player.Session == null)
            {
                CLog.Warning(R_N_CONCMD_PL, argv[1]);
                return true;
            }

            // Concatène le message à partir de argv[2]
            var message = ConcatArgs(argc, 1, argv);
            if (player.Session is WorkerServerSocket session)
            {
                // Envoi du message privé via WorldPacket
                var packet = new WorldPacket((int)SMSG_MESSAGECHAT);
                packet.Write((byte)3); // Type: Whisper (3)
                packet.WriteUInt32(0); // Langue: 0 = universel
                packet.WriteString("Console"); // Expéditeur
                packet.WriteString(player.Name); // Destinataire
                packet.WriteString(message); // Texte
                packet.Write((byte)0); // Flags additionnels (optionnel)
                session.SendPacket(packet);
                console.Write("Message envoyé à {0}.", player.Name);
            }
            else
            {
                CLog.Error("Session non valide pour le joueur {0}.", player.Name);
            }
            return true;
        }

        // Affiche la liste des GMs connectés
        public static bool HandleGMsCommand(IConsole console, int argc, string[] argv)
        {
            console.Write(R_N_CONCMD_G);
            console.Write("======================================================\r\n");
            console.Write("| {0,21} | {1,15} | {2,4}  |\r\n", R_N_CONCMD_N, R_N_CONCMD_P, R_N_CONCMD_L);
            console.Write("======================================================\r\n");

            foreach (var player in ClientMgr.Instance.GetAllPlayers())
            {
                if (!string.IsNullOrEmpty(player.GMPermissions))
                {
                    console.Write("| {0,21} | {1,15} | {2,4} ms |\r\n", player.Name, player.GMPermissions, player.Latency);
                }
            }
            console.Write("======================================================\r\n\r\n");
            return true;
        }

        // Affiche la liste des joueurs connectés
        public static bool HandleOnlinePlayersCommand(IConsole console, int argc, string[] argv)
        {
            console.Write(R_N_CONCMD_J);
            console.Write("======================================================\r\n");
            console.Write("| {0,21} | {1,15} | {2,4}  |\r\n", R_N_CONCMD_N, R_N_CONCMD_LVL, R_N_CONCMD_L);
            console.Write("======================================================\r\n");

            foreach (var player in ClientMgr.Instance.GetAllPlayers())
            {
                console.Write("| {0,21} | {1,15} | {2,4} ms |\r\n", player.Name, player.Level, player.Latency);
            }
            console.Write("======================================================\r\n\r\n");
            return true;
        }

        // Affiche les infos d'un joueur (HandlePlayerInfoCommand)
        public static bool HandlePlayerInfoCommand(IConsole console, int argc, string[] argv)
        {
            if (argc < 2)
                return false;

            var player = ClientMgr.Instance.GetRPlayer(argv[1]);
            if (player == null || player.Session == null)
            {
                console.Write(R_N_CONCMD_PC);
                return true;
            }

            console.Write(R_N_CONCMD_PC_1, player.Name);
            console.Write(R_N_CONCMD_PC_2, player.Race);
            console.Write(R_N_CONCMD_PC_3, player.Class);
            var session = player.Session;
            var socket = (session as Session)?.GetSocket();
            // Cast socket to the correct type (e.g., SocketType) before calling GetRemoteIP()
            var remoteIp = R_N_CONCMD_PC_5;
            if (socket != null)
            {
                // Replace 'SocketType' with the actual type that has GetRemoteIP()
                if (socket is Socket typedSocket)
                {
                    remoteIp = typedSocket.GetRemoteIP();
                }
            }
            console.Write(R_N_CONCMD_PC_4, remoteIp);
            console.Write(R_N_CONCMD_PC_6, player.Level);
            console.Write(R_N_CONCMD_PC_7, (session as Session)?.GetAccountName());
            return true;
        }

        // Affiche les infos serveur (HandleInfoCommand)
        public static bool HandleInfoCommand(IConsole console, int argc, string[] argv)
        {
            int gm = 0, count = 0, avg = 0, alliance = 0, horde = 0;
            foreach (var player in ClientMgr.Instance.GetAllPlayers())
            {
                count++;
                avg += (int)player.Latency;
                if (!string.IsNullOrEmpty(player.GMPermissions))
                    gm++;
                if (player.Team != 0)
                    horde++;
                else
                    alliance++;
            }
            console.Write("======================================================================\r\n");
            console.Write(R_N_CONCMD_I);
            console.Write("======================================================================\r\n");
            console.Write(R_N_CONCMD_I_1, BRANCH_NAME, REVISION, CONFIG, PLATFORM_TEXT, ARCH);
            console.Write(R_N_CONCMD_I_2, "UptimeString"); // TODO: Replace with real uptime
            console.Write(R_N_CONCMD_I_3, count, gm, 0);
            console.Write(R_N_CONCMD_I_4, alliance);
            console.Write(R_N_CONCMD_I_5, horde);
            console.Write(R_N_CONCMD_I_6, 0); // TODO: ThreadPool.GetActiveThreadCount()
            console.Write(R_N_CONCMD_I_7, 0); // TODO: ThreadPool.GetFreeThreadCount()
            console.Write(R_N_CONCMD_I_8, count > 0 ? ((float)avg / count) : 0.0f);
            console.Write(R_N_CONCMD_I_9, 0); // TODO: WorldDatabase.GetQueueSize()
            console.Write(R_N_CONCMD_I_10, 0); // TODO: CharacterDatabase.GetQueueSize()
            console.Write("======================================================================\r\n\r\n");
            return true;
        }

        // Annonce globale (HandleAnnounceCommand)
        public static bool HandleAnnounceCommand(IConsole console, int argc, string[] argv)
        {
            if (argc < 2)
                return false;

            var message = ConcatArgs(argc, 0, argv);
            foreach (var player in ClientMgr.Instance.GetAllPlayers())
            {
                if (player.Session is WorkerServerSocket session)
                {
                    var packet = new WorldPacket((int)SMSG_MESSAGECHAT);
                    packet.Write((byte)1); // Type: Annonce (1)
                    packet.WriteUInt32(0); // Langue: 0 = universel
                    packet.WriteString("Console"); // Expéditeur
                    packet.WriteString(player.Name); // Destinataire
                    packet.WriteString(message); // Texte
                    packet.Write((byte)0); // Flags additionnels (optionnel)
                    session.SendPacket(packet);
                }
            }
            console.Write("Annonce envoyée à tous les joueurs.");
            return true;
        }

        // Annonce globale écran (HandleWAnnounceCommand)
        public static bool HandleWAnnounceCommand(IConsole console, int argc, string[] argv)
        {
            if (argc < 2)
                return false;
            // TODO: Envoyer l'annonce à tous les joueurs (wide screen)
            console.Write(R_N_CONCMD_W);
            return true;
        }

        // Kick un joueur (HandleKickCommand)
        public static bool HandleKickCommand(IConsole console, int argc, string[] argv)
        {
            if (argc < 3)
                return false;
            var player = ClientMgr.Instance.GetRPlayer(argv[1]);
            if (player == null || player.Session == null)
            {
                console.Write(R_N_CONCMD_PL, argv[1]);
                return true;
            }
            var session = player.Session as Session;
            var socket = session?.GetSocket();
            if (socket is Socket typedSocket)
            {
                typedSocket.Disconnect();
            }
            else
            {
                CLog.Error("Impossible de déconnecter le joueur {0} : socket non valide.", player.Name);
            }
            console.Write(R_N_CONCMD_PL_1, player.Name);
            return true;
        }

        // Affiche le motd (HandleMOTDCommand)
        public static bool HandleMOTDCommand(IConsole console, int argc, string[] argv)
        {
            console.Write(R_N_CONCMD_W);
            return true;
        }

        // Affiche l'uptime (HandleUptimeCommand)
        public static bool HandleUptimeCommand(IConsole console, int argc, string[] argv)
        {
            // TODO: Récupérer la vraie uptime
            console.Write(R_N_CONCMD_I_2, "UptimeString");
            return true;
        }
        // Affiche l'aide des commandes console
        // public static bool HandleHelpCommand(IConsole console, int argc, string[] argv)
        // {
        //     console.Write("Commandes disponibles :\r\n");
        //     console.Write("banaccount <compte> <durée> : Bannir un compte pour une durée en secondes (0 = permanent)");
        //     console.Write("unbanaccount <compte> : Débannir un compte");
        //     console.Write("backupdb : Sauvegarder la base de données");
        //     console.Write("cancel : Annuler le shutdown en cours");
        //     console.Write("createaccount <login> <password> <email> <gm> <flags> : Créer un compte joueur");
        //     console.Write("resetpassword <compte> <nouveau_mdp> <confirmation> : Réinitialiser le mot de passe d'un compte");
        //     console.Write("rehash : Recharger la configuration serveur");
        //     console.Write("saveall : Sauvegarder tous les joueurs connectés");
        //     console.Write("shutdown [délai] : Arrêter le serveur après un délai en secondes (défaut 5)");
        //     console.Write("whisper <joueur> <message> : Envoyer un message privé à un joueur");
        //     console.Write("gms : Afficher la liste des GMs connectés");
        //     console.Write("online : Afficher la liste des joueurs connectés");
        //     console.Write("playerinfo <joueur> : Afficher les infos d'un joueur");
        //     console.Write("info : Afficher les infos serveur");
        //     console.Write("announce <message> : Envoyer une annonce globale à tous les joueurs");
        //     console.Write("wannounce <message> : Envoyer une annonce globale en écran large");
        //     console.Write("kick <joueur> <motif> : Expulser un joueur avec un motif");
        //     console.Write("motd : Afficher le message du jour");
        //     console.Write("uptime : Afficher l'uptime du serveur");
        //     return true;
        // }

        // Utilitaire pour concaténer les arguments (équivalent à ConcatArgs)
        // Concatène les arguments à partir d'un offset (utilitaire)
        public static string ConcatArgs(int argc, int startOffset, string[] argv)
        {
            var outstr = string.Empty;
            for (int i = startOffset + 1; i < argc; ++i)
            {
                outstr += argv[i];
                if ((i + 1) != argc)
                    outstr += " ";
            }
            return outstr;
        }
    }
}

