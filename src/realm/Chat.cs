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

using WaadShared;

namespace WaadRealmServer;

public class ChatHandler
{
    public ChatHandler() { }

    private static WorldPacket FillSystemMessageData(string message)
    {
        ulong guid = 0;
        uint messageLength = (uint)(message.Length + 1);
        int packetSize = 30 + message.Length + 1;

        var packet = new WorldPacket((int)Opcodes.SMSG_MESSAGECHAT, packetSize);
        packet.WriteByte((byte)ChatMsg.CHAT_MSG_SYSTEM); // type
        packet.WriteUInt32((uint)Languages.LANG_UNIVERSAL); // language
        packet.WriteUInt64(guid); // guid
        packet.WriteUInt32(0); // unk
        packet.WriteUInt64(guid); // guid again
        packet.WriteUInt32(messageLength); // message length
        packet.WriteString(message); // message
        packet.WriteByte(0); // flag
        return packet;
    }

    // Remplissage d'un message de chat (stub, à compléter avec WorldPacket réel)
    public static WorldPacket FillMessageData(uint type, int language, string message, ulong guid, byte flag = 0)
    {
        uint messageLength = (uint)(message.Length + 1);
        int packetSize = 30 + message.Length + 1;

        var packet = new WorldPacket((int)Opcodes.SMSG_MESSAGECHAT, packetSize);
        packet.WriteByte((byte)type); // type
        packet.WriteUInt32((uint)language); // language
        packet.WriteUInt64(guid); // guid
        packet.WriteUInt32(0); // unk
        packet.WriteUInt64(guid); // guid again
        packet.WriteUInt32(messageLength); // message length
        packet.WriteString(message); // message
        packet.WriteByte(flag); // flag
        return packet;
    }

    // Analyse des commandes de chat (stub)
    public static int ParseCommands(string text, Session session)
    {
        // TODO: Implémenter le parsing des commandes serveur (voir Chat.cpp)
        if (string.IsNullOrEmpty(text) || session == null)
            return 0;
        if (!(text.StartsWith('!') || text.StartsWith('.')))
            return 0;
        if (text.Length > 1 && text[1] == '.')
            return 0;
        // TODO: Passer la commande au gestionnaire de commandes réel
        return 1;
    }

    public static void SystemMessage(WorkerServerSocket m_session, string message, params object[] args)
    {
        if (string.IsNullOrEmpty(message)) return;
        string msg1 = string.Format(message, args);
        var data = FillSystemMessageData(msg1);
        m_session?.SendPacket(data);
    }

    public static void ColorSystemMessage(WorkerServerSocket m_session, string colorcode, string message, params object[] args)
    {
        if (string.IsNullOrEmpty(message)) return;
        string msg1 = string.Format(message, args);
        string msg = $"{colorcode}{msg1}|r";
        var data = FillSystemMessageData(msg);
        m_session?.SendPacket(data);
    }

    public static void RedSystemMessage(WorkerServerSocket m_session, string message, params object[] args)
    {
        if (string.IsNullOrEmpty(message)) return;
        string msg1 = string.Format(message, args);
        string msg = $"|cffff6060{msg1}|r";
        var data = FillSystemMessageData(msg);
        m_session?.SendPacket(data);
    }

    public static void GreenSystemMessage(WorkerServerSocket m_session, string message, params object[] args)
    {
        if (string.IsNullOrEmpty(message)) return;
        string msg1 = string.Format(message, args);
        string msg = $"|cff00ff00{msg1}|r";
        var data = FillSystemMessageData(msg);
        m_session?.SendPacket(data);
    }

    public static void BlueSystemMessage(WorkerServerSocket m_session, string message, params object[] args)
    {
        if (string.IsNullOrEmpty(message)) return;
        string msg1 = string.Format(message, args);
        string msg = $"|cff00ccff{msg1}|r";
        var data = FillSystemMessageData(msg);
        m_session?.SendPacket(data);
    }

    public static void RedSystemMessageToPlr(RPlayerInfo plr, string message, params object[] args)
    {
        if (string.IsNullOrEmpty(message) || plr?.Session == null) return;
        RedSystemMessage((WorkerServerSocket)plr.Session, message, args);
    }

    public static void GreenSystemMessageToPlr(RPlayerInfo plr, string message, params object[] args)
    {
        if (string.IsNullOrEmpty(message) || plr?.Session == null) return;
        GreenSystemMessage((WorkerServerSocket)plr.Session, message, args);
    }

    public static void BlueSystemMessageToPlr(RPlayerInfo plr, string message, params object[] args)
    {
        if (string.IsNullOrEmpty(message) || plr?.Session == null) return;
        BlueSystemMessage((WorkerServerSocket)plr.Session, message, args);
    }

    public static void SystemMessageToPlr(RPlayerInfo plr, string message, params object[] args)
    {
        if (string.IsNullOrEmpty(message) || plr?.Session == null) return;
        SystemMessage((WorkerServerSocket)plr.Session, message, args);
    }

    protected static void SendMultilineMessage(WorkerServerSocket m_session, string str)
    {
        if (string.IsNullOrEmpty(str)) return;
        var lines = str.Split('\n');
        foreach (var line in lines)
        {
            if (!string.IsNullOrEmpty(line))
                SystemMessage(m_session, line);
        }
    }
}
