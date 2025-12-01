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
using System.Text;
using WaadShared;

using static WaadShared.ChatHandler;

namespace WaadRealmServer;

public static class LanguageSkills
{
    // Index = (int)Languages enum
    public static readonly uint[] Skills =
    [
        0,    //  0 - UNIVERSAL
        109,  //  1 - ORCISH
        113,  //  2 - DARNASSIAN
        115,  //  3 - TAURAHE
        0,    //  4 - Unused
        0,    //  5 - Unused
        111,  //  6 - DWARVISH
        98,   //  7 - COMMON
        139,  //  8 - DEMON TONGUE
        140,  //  9 - TITAN
        137,  // 10 - THALSSIAN
        138,  // 11 - DRACONIC
        0,    // 12 - KALIMAG
        313,  // 13 - GNOMISH
        315,  // 14 - TROLL
        0,    // 15
        0,    // 16
        0,    // 17
        0,    // 18
        0,    // 19
        0,    // 20
        0,    // 21
        0,    // 22
        0,    // 23
        0,    // 24
        0,    // 25
        0,    // 26
        0,    // 27
        0,    // 28
        0,    // 29
        0,    // 30
        0,    // 31
        0,    // 32
        673,  // 33 - Bas-parler / GutterSpeak
        0,    // 34
        759,  // 35 - Dranei
        0,    // 36 - Zombie (Pointillés)
        0,    // 37 - Gnome (Code Binaire)
        0     // 38 - Goblin (Code Binaire)
    ];
}

public partial class Session
{
    // Propriétés principales
    private bool IsMuted { get; set; }
    private uint MutedUntil { get; set; }
    private RPlayerInfo PlayerInfo { get; set; }
    private Session ISession { get; set; }

    // Méthodes d'instance
    private void SendPacket(object data)
    {
        if (ISession != null && data is WorldPacket packet)
            Wss.SendPacket(packet);
        else
            CLog.Warning("[SendPacket]", $"{data}");
    }

    private void SystemMessage(string message)
    {
        if (ISession != null)
            ChatHandler.SystemMessage(Wss, message);
        else
            CLog.Warning("[SystemMessage]", $"{message}");
    }

    // Méthode utilitaire : broadcast local ou via session
    private void RBroadcastMessage(string format, params object[] args)
    {
        string message = string.Format(format, args);
        if (ISession != null)
            ChatHandler.SystemMessage(Wss, message);
        else
            Console.WriteLine(message);
    }

    // Méthodes statiques utilitaires
    private static uint GetUnixTime() => (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static RPlayerInfo RPlayerInfoLookup(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return ClientMgr.Instance.GetRPlayer(name);
    }

    private void HandleMessagechatOpcode(WorldPacket recvData)
    {
        var sLog = new Logger();
        var SizeOfHandleMessagechatPacket = 9;
        if (!Utils.CheckPacketSize(recvData, SizeOfHandleMessagechatPacket, this))
        {
            sLog.OutError("CHAT", R_E_CHARHAN_COM, recvData.Size, SizeOfHandleMessagechatPacket);
            return;
        }

        // 1. Lire les données du paquet
        uint type;
        int lang;
        string msg;
        string misc;
        try
        {
            type = recvData.ReadUInt32();
            lang = recvData.ReadInt32();
            msg = recvData.ReadString() ?? string.Empty;
            misc = recvData.ReadString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            sLog.OutError("CHAT", R_E_CHARHAN_COM_1, ex.Message);
            Disconnect();
            return;
        }

        // 2. Vérifier la validité de la langue
        if (lang < 0 || lang >= LanguageSkills.Skills.Length)
        {
            sLog.OutDebug("CHAT", R_D_CHATHAN_1, lang);
            return;
        }

        // 3. Vérification du mute
        if ((type == (int)ChatMsg.CHAT_MSG_WHISPER || type == (int)ChatMsg.CHAT_MSG_CHANNEL) && IsMuted && MutedUntil >= GetUnixTime())
        {
            var player = RPlayerInfoLookup(misc);
            if (player != null)
            {
                uint timeLeft = MutedUntil > GetUnixTime() ? MutedUntil - GetUnixTime() : 0;
                RBroadcastMessage(MUTED_MESSAGE);
                RBroadcastMessage(MUTED_TIME_LEFT, timeLeft);
                return;
            }
        }

        // 4. Anti-spam et validation des messages
        if (msg.Contains("|TInterface") || msg.Contains('\n'))
        {
            SystemMessage(ALERT_MESSAGE);
            return;
        }

        if (msg.Contains("|c") && !msg.Contains("|H"))
            return;

        // 5. Détection de motifs malveillants
        byte[] msgBytes = Encoding.UTF8.GetBytes(msg);
        if (MaliciousPatternDetector.ContainsMaliciousPatterns(msgBytes))
        {
            SystemMessage(ALERT_MESSAGE_1);
            return;
        }

        // 6. Traitement en fonction du type de message
        switch (type)
        {
            case (int)ChatMsg.CHAT_MSG_WHISPER:
                HandleWhisperMessage(recvData, type, lang, msg, misc, sLog);
                break;
            case (int)ChatMsg.CHAT_MSG_CHANNEL:
                HandleChannelMessage(recvData, type, lang, msg, misc, sLog);
                break;
            default:
                sLog.OutDebug("CHAT", R_D_CHATHAN, type, lang, msg);
                break;
        }
    }

    private void HandleWhisperMessage(WorldPacket recvData, uint type, int lang, string msg, string misc, Logger sLog)
    {
        var player = RPlayerInfoLookup(misc);
        if (player == null)
        {
            if (misc.Equals("console", StringComparison.OrdinalIgnoreCase))
            {
                sLog.OutDebug("Whisper", $"{GetPlayer()} to {misc}: {msg}");
                return;
            }
            else
            {
                SendPacket(string.Format(PLAYER_NOT_FOUND, misc));
                return;
            }
        }

        // Vérifier permissions GM et envoyer réponse automatique MJ
        if (PlayerInfo != null && string.IsNullOrEmpty(PlayerInfo.GMPermissions) && !string.IsNullOrEmpty(player.GMPermissions))
        {
            string reply = REPLY_MESSAGE;
            SendPacket($"[AutoGM] {reply}");
            return;
        }

        if (lang > 0 && LanguageSkills.Skills[lang] != 0)
            return;

        if (lang == 0 && !CanUseCommand("c"))
            return;

        // Vérifier si le joueur destinataire ignore l'expéditeur
        if (player.Social_IsIgnoring(PlayerInfo?.Guid ?? 0))
        {
            SendPacket($"[Ignored] {msg}");
            return;
        }

        // Envoi du message whisper (paquet réel)
        if (player.Session is WorkerServerSocket sessionDest && PlayerInfo != null)
        {
            var packet = ChatHandler.FillMessageData(
                (uint)ChatMsg.CHAT_MSG_WHISPER,
                (CanUseCommand("c") || ISession.CanUseCommand("c")) && lang != -1 ? (int)Languages.LANG_UNIVERSAL : lang,
                msg,
                PlayerInfo.Guid,
                !string.IsNullOrEmpty(PlayerInfo.GMPermissions) ? (byte)4 : (byte)0
            );
            sessionDest.SendPacket(packet);
        }

        // Envoi du message inform (retour à l'expéditeur)
        if (player != null && PlayerInfo != null && player.Session != null)
        {
            var informPacket = ChatHandler.FillMessageData(
                (uint)ChatMsg.CHAT_MSG_WHISPER_INFORM,
                (int)Languages.LANG_UNIVERSAL,
                msg,
                player.Guid,
                !string.IsNullOrEmpty(player.GMPermissions) ? (byte)4 : (byte)0
            );
            SendPacket(informPacket);
        }
    }

    private void HandleChannelMessage(WorldPacket recvData, uint type, int lang, string msg, string misc, Logger sLog)
    {
        // Commande serveur ?
        if (ChatHandler.ParseCommands(msg, ISession) > 0)
            return;

        var player = GetPlayer();
        var chn = ChannelMgr.GetChannel(misc, player);
        if (chn != null && player != null)
        {
            chn.Say(player, msg, null, false);
        }
        else
        {
            sLog.OutDebug("Channel", $"{misc}: {msg}");
        }
    }
}