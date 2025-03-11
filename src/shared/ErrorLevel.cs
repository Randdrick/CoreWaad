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

namespace WaadShared;

public static class AccountCache
{
    public const string L_N_ACCOUNT = "[AccountMgr] Rechargement des comptes...";
    public const string L_N_ACCOUNT_F = "[AccountMgr] %u comptes trouvés.";
    public const string L_W_ACCOUNT_F = ">> Suppression d'un compte en double %u [%s]...\n";
    public const string L_D_ACCOUNT_B = "[AccountMgr] Le ban du compte %s a expiré.";
    public const string L_D_ACCOUNT_M = "[AccountMgr] La mise en silence du compte %s a expiré.";
    public const string L_P_ACCOUNT_I = "[AccountMgr] IP ban \"%s\" non indiqué. Supposition /32";
    public const string L_P_ACCOUNT_I_1 = "[AccountMgr] IP ban \"%s\" ne peut être analysé. Ignore";
    public const string L_E_ACCOUNT_S = "[AccountMgr] Fermeture du socket en raison de l'expiration du ping.\n";
    public const string L_E_ACCOUNT_S_1 = "[AccountMgr] Socket déconnecté : %s en raison d'une IP qui n'est plus allouée.\n";
}

public static class AuthSocket
{
    public const string L_N_AUTHSOCK = "[AuthChallenge] Réception Ok (%u octets)";
    public const string L_N_AUTHSOCK_1 = "[AuthChallenge] Reconnexion. (%u octets reçus)";
    public const string L_N_AUTHSOCK_2 = "Commande inconnue %u";
    public const string L_D_AUTHSOCK_1 = "Transfert accepté";
    public const string L_D_AUTHSOCK_2 = "Reprise du transfert";
    public const string L_D_AUTHSOCK_C = "[AuthChallenge] Déconnexion.";
    public const string L_D_AUTHSOCK_C_1 = "[AuthChallenge] Réception terminée.";
    public const string L_D_AUTHSOCK_C_2 = "Patch %u%s sélectionné pour le client.";
    public const string L_D_AUTHSOCK_C_3 = "[AuthChallenge] Nom du compte: \"%s\"";
    public const string L_D_AUTHSOCK_C_4 = "[AuthChallenge] Compte invalide.";
    public const string L_D_AUTHSOCK_C_5 = "[AuthChallenge] Etat du compte : Banni définitivement = %u";
    public const string L_D_AUTHSOCK_C_6 = "[AuthChallenge] Le SALT n'est pas valide";
    public const string L_D_AUTHSOCK_C_7 = "[AuthChallenge] La vérification est incorrecte";
    public const string L_D_AUTHSOCK_C_8 = "[AuthChallenge] Trame Ok.";
    public const string L_D_AUTHSOCK_C_9 = "[AuthChallenge] Nom du compte: \"%s\"";
    public const string L_D_AUTHSOCK_C_10 = "[AuthChallenge] Etat du compte : Banni = %u";
    public const string L_D_AUTHSOCK_P = "[AuthLogonProof] les valeurs M1 ne correspondent pas.";
    public const string L_D_AUTHSOCK_P_1 = "[AuthLogonProof] Initialisation du Patch-Job";
    public const string L_D_AUTHSOCK_P_2 = "[AuthLogonProof] Intercalation et vérification de preuve...";
    public const string L_D_AUTHSOCK_P_3 = "[AuthLogonProof] Identification réussie.";
    public const string L_N_AUTHSOCK_P = "# Nom du compte!\n";
}

public static class AutoPatcher
{
    public const string L_N_AUTPATCH = "Chargement des patches...";
    public const string L_N_AUTPATCH_1 = "Patch trouvé pour b%u locale `%s`.";
}

public static class LogonConsole
{
    public const string L_N_LOGONCON = "Fichier de configuration rechargé...";
    public const string L_N_LOGONCON_1 = "Attente de la fin du processus de la console....\n";
    public const string L_N_LOGONCON_2 = "Arrêt de la Console.\n";
    public const string L_N_LOGONCON_3 = "Interprète de la Console";
    public const string L_N_LOGONCON_4 = "Console: Commande inconnue (utiliser \"help\" pour l'aide).\n";
    public const string L_N_LOGONCON_5 = "Console: --------aide--------";
    public const string L_N_LOGONCON_6 = "   help, ?: affiche ce texte";
    public const string L_N_LOGONCON_7 = "   reload: recharge les comptes";
    public const string L_N_LOGONCON_8 = "   shutdown, exit: ferme le programme";
}

public static class LogonCommServer
{
    public const string L_N_LOGCOMSE = "Connection au serveur pour %s:%u refusée, IP non autorisée.\n";
    public const string L_N_LOGCOMSE_1 = "Réception un paquet inconnu du LogonServer : %u\n";
    public const string L_N_LOGCOMSE_2 = "Enregistrement du royaume `%s` sous l'ID %u.";
    public const string L_N_LOGCOMSE_3 = "Demande d'identification de %s, resultat %s.";
    public const string L_N_LOGCOMSE_4 = "Échec de la décompression du mapping.\n";
    public const string L_N_LOGCOMSE_5 = "Info: Royaume numéro %u, Comptes : %u\n";
    public const string L_N_LOGCOMSE_6 = "Clef: ";
    public const string L_D_LOGCOMSE_L = "[LogonCommServer] Testing console login: %s\n";
    public const string L_E_LOGCOMSE_L_1 = "Demande de modification de la base de données %u refusée pour %s !\n";
    public const string L_E_LOGCOMSE_R = "[RemoteConsole] Player %s Flag: %s , Connection non permise !\n";
}

public static class Main
{
    public const string L_N_MAIN = "Les paramètres de connexion à la base de données Logon sont invalides.";
    public const string L_N_MAIN_1 = "Le fichier de configuration ne peut pas être rechargé.";
    public const string L_W_MAIN_2 = "IP: %s ne peut être analysée et sera donc ignorée";
    public const string L_N_MAIN_3 = "Vérification de la configuration: %s";
    public const string L_N_MAIN_4 = "  Passée sans erreurs.";
    public const string L_N_MAIN_5 = "  Une ou plusieurs erreurs ont été rencontrées.";
    public const string L_N_MAIN_6 = "Directive die interceptée. Vous devez effacer die et die2 de votre fichier de configuration avant de continuer.";
    public const string L_N_MAIN_7 = "Appuyer sur les touches <Ctrl + C> pour permettre l'arrêt du serveur en toute sécurité.";
    public const string L_N_MAIN_8 = "Chargement de la configuration...";
    public const string L_N_MAIN_9 = "Démarrage...";
    public const string L_N_MAIN_10 = "Pré-chargement des comptes...";
    public const string L_N_MAIN_11 = "%u comptes chargés et prêts.";
    public const string L_N_MAIN_12 = "Succès ! Prêt pour les connexions";
    public const string L_N_MAIN_13 = "Fermeture...";
    public const string L_N_MAIN_14 = "Fermeture de la base de données en cours...";
    public const string L_N_MAIN_15 = "Arrêt du serveur";
    public const string L_E_MAIN = "Échec de l'initialisation de la base de données LogonServer";
    public const string L_W_MAIN_AI = "Désactivé, aucun contrôle ne sera fait....";
    public const string L_W_MAIN_AI_1 = "Activé";
}

public static class Channel
{
    public const string R_N_CHANNEL = "'%s', vous devez être au niveau %u pour parler dans ce canal.";
}

public static class ChannelHandler
{
    public const string R_N_CHANHAN = "Problème de connexion. Annulation de la requête en cours";
    public const string R_D_CHANHAN = "%s, canal numéro : %u";
}

public static class ChatHandler
{
    public const string R_N_CHATHAN = "CHAT: Message inconnu - Type %u, langue : %u";
}

public static class CharacterHandler
{
    public const string R_E_CHARHAN_PL = "Le personnage n'existe pas dans la base de données !";
    public const string R_E_CHARHAN_PL_1 = "L'instance a été supprimée ou n'est plus valide. Tentative de reconnexion à la Map %u";
    public const string R_E_CHARHAN_PL_2 = "Échec de la reconnexion à la Map %u. Instance non trouvée.";
    public const string R_E_CHARHAN_PL_3 = "Échec de la reconnexion à la Map %u. Pas d'information.";
    public const string R_E_CHARHAN_PL_4 = "Aucune instance trouvée. Tentative de reconnexion en cours";
    public const string R_E_CHARHAN_PL_5 = "Le serveur de 'Monde' est hors ligne !";
    public const string R_E_CHARHAN_BR = "AccountId du joueur (%u) non trouvé dans la table BlizzRequirements <- Reportez ceci aux développeurs.";
    public const string R_N_CHARHAN_BR = "%s veut créer un DK mais il en possède déjà un !";
    public const string R_N_CHARHAN_BR_1 = "%s veut créer un DK mais il n'a aucun personnage de niveau > à 55 !";
    public const string R_E_CHARHAN_CC = "Il n'y a pas de serveur de 'Monde' en ligne pour la création du personnage";
    public const string R_E_CHARHAN_CC_1 = "Il n'existe pas de session avec cet id de compte: %u";
    public const string R_D_CHARHAN_CD = "L'OPCODE est %u";
    public const string R_D_CHARHAN_CD_1 = "Le guid est %u";
    public const string R_D_CHARHAN_CD_2 = "Son AccountID est %u";
    public const string R_D_CHARHAN_CD_3 = "L'info de Session est %u";
    public const string R_D_CHARHAN_CD_4 = "Ses références sont %u";
    public const string R_W_CHARHAN_CD = "Ne trouve pas le personnage par la recherche du Guid %u et de son AccountID %u";
    public const string R_W_CHARHAN_CD_1 = "Le personnage est le chef de la Guilde : %u";
    public const string R_W_CHARHAN_CD_2 = "Le personnage est le meneur de l'équipe : %u";
    public const string R_W_CHARHAN_CD_3 = "Il n'y a pas de serveur de 'Monde' en ligne pour réaliser l’opération";
    public const string R_W_CHARHAN_CD_4 = "Le personnage n'existe pas ou a déjà été supprimé";
    public const string R_W_CHARHAN_CD_5 = "Le personnage n'existe pas dans le serveur de 'Monde'";
    public const string R_W_CHARHAN_UA = "ATTENTION: Accountdata > 8. La mise à jour de (%d) a été demandée par le compte %s (%u) !";
    public const string R_W_CHARHAN_UA_1 = "ATTENTION: Échec de la décompression des données du compte %d pour %s.";
    public const string R_W_CHARHAN_UA_2 = "ATTENTION: La décompression a généré une erreur inconnue: %x, des données du compte %d pour %s. ÉCHEC.";
    public const string R_N_CHARHAN_UA = "WORLD: Décompression réussie des données du compte %d pour %s et mise à jour de la matrice de stockage.";
    public const string R_E_CHARHAN_UA = "Erreur lors de la compression de ACCOUNT_DATA.";
}

public static class QueryHandler
{
    public const string R_E_QUERHAN_IT = "WORLD: Objet inconnu : id 0x%.8X";
    public const string R_E_QUERHAN_IT_1 = "Objet inconnu";
    public const string R_D_QUERHAN = "[Session] Réception de CMSG_NAME_QUERY pour: %s";
}

public static class Session
{
    public const string R_E_SESSION = "Erreur Fatale: Packet en erreur (NULL)";
    public const string R_E_SESSION_1 = "[Session] : Réception d'un paquet hors limite avec l'opcode %s (0x%04X)";
    public const string R_D_SESSION = "[Session] : Traitement de l'Opcode par le RealmServeur %s (0x%04X)";
    public const string R_D_SESSION_1 = "[Session] : Traitement envoyé au WorldServer";
    public const string R_D_SESSION_2 = "[Session] : Réception d'un paquet non géré avec l'opcode: %s (0x%04X)";
}

public static class WorldSocket
{
    public const string R_W_WRDSOCK = "ATTENTION: Tentative d'envoi d'un paquet de%u octets (trop volumineux) à un socket. Le code d'opération (OPCODE) était:%u (0x%03X).\n";
    public const string R_W_WRDSOCK_1 = "ATTENTION: Problème dans la réception du paquet. Tentative de récupération.\n";
    public const string R_E_WRDSOCK = "Erreur dans la réception du paquet";
    public const string R_E_WRDSOCK_1 = "Copie incomplète de AUTH_SESSION reçue.";
    public const string R_E_WRDSOCK_2 = "Erreur ! Client en double.";
    public const string R_E_WRDSOCK_3 = "m_session est null. Le chargement ne peut pas continuer";
    public const string R_E_WRDSOCK_4 = "Section d'Addon en erreur";
    public const string R_D_WRDSOCK = "Il y a une permission forcée pour le compte No. %u (%s)";
    public const string R_D_WRDSOCK_1 = "[WorldSocket] : Réception de l'Opcode:0x%04X";
    public const string R_D_WRDSOCK_2 = "[WorldSocket] : OnReadPacket 0x%04X";
    public const string R_D_WRDSOCK_3 = "ATTENTION: Envoie inachevé de session auth.";
    public const string R_D_WRDSOCK_4 = "Section d'Addon Ok";
    public const string R_N_WRDSOCK = "Réception des informations du compte: `%s` - Session ID %u (Requête %u)";
    public const string R_N_WRDSOCK_1 = "%s depuis %s:%u [%ums]";
    public const string R_N_WRDSOCK_2 = "Socket fermé en raison d'un paquet ping incomplet.";
}

public static class ConsoleCommands
{
    public const string R_N_CONCMD_I = "Informations sur le serveur: \r\n";
    public const string R_N_CONCMD_I_1 = "Révision du Core: Waad %s r%u/%s-%s-%s\r\n(https://arbonne.games-rpg.net)\r\n";
    public const string R_N_CONCMD_I_2 = "Serveur en ligne depuis: %s\r\n";
    public const string R_N_CONCMD_I_3 = "Joueurs en lignes: %u (%d GMs, %d en attente)\r\n";
    public const string R_N_CONCMD_I_4 = "Joueurs Alliance en lignes: %u\r\n";
    public const string R_N_CONCMD_I_5 = "Joueurs Hordre en lignes: %u\r\n";
    public const string R_N_CONCMD_I_6 = "Nombre de Threads actifs: %u\r\n";
    public const string R_N_CONCMD_I_7 = "Nombre de Threads libres: %u\r\n";
    public const string R_N_CONCMD_I_8 = "Latence moyenne: %.3fms\r\n";
    public const string R_N_CONCMD_I_9 = "Taille du cache SQL (World): %u requêtes retardées\r\n";
    public const string R_N_CONCMD_I_10 = "Taille du cache SQL (Character): %u requêtes retardées\r\n";
    public const string R_N_CONCMD_G = "Les GMs suivants sont connectés sur le serveur : \r\n";
    public const string R_N_CONCMD_J = "Les joueurs suivants sont connectés sur le serveur : \r\n";
    public const string R_N_CONCMD_N = "Nom";
    public const string R_N_CONCMD_P = "Permissions";
    public const string R_N_CONCMD_L = "Latence";
    public const string R_N_CONCMD_LVL = "Niveau";
    public const string R_N_CONCMD_W = "Commande disponible dans la console du World uniquement !";
    public const string R_N_CONCMD_PL = "Ne trouve pas le joueur, %s.\r\n";
    public const string R_N_CONCMD_PL_1 = "Le joueur %s a été renvoyé.\r\n";
    public const string R_N_CONCMD_S = "Arrêt initié.\r\n";
    public const string R_N_CONCMD_S_1 = "Arrêt annulé.\r\n";
    public const string R_N_CONCMD_A = "Le compte '%s' a été banni %s%s. Les changements seront effectifs avec le prochain cycle de rechargement.\r\n";
    public const string R_N_CONCMD_A_1 = "jusqu'à ";
    public const string R_N_CONCMD_A_2 = "pour toujours";
    public const string R_N_CONCMD_A_3 = "Le compte '%s' a été débanni.\r\n";
    public const string R_N_CONCMD_A_4 = "Compte créé.\r\n";
    public const string R_N_CONCMD_A_5 = "Erreur rencontrée lors de la création de compte. Veuillez vérifier les informations entrées.\r\n";
    public const string R_N_CONCMD_A_6 = "Le compte %s n'existe pas.\r\n";
    public const string R_N_CONCMD_PW = "Les mots de passe ne correspondent pas.\r\n";
    public const string R_N_CONCMD_PW_1 = "Mot de passe réinitialisé.\r\n";
    public const string R_N_CONCMD_PC = "Joueur non trouvé.\r\n";
    public const string R_N_CONCMD_PC_1 = "Joueur: %s\r\n";
    public const string R_N_CONCMD_PC_2 = "Race: %s\r\n";
    public const string R_N_CONCMD_PC_3 = "Classe: %s\r\n";
    public const string R_N_CONCMD_PC_4 = "IP: %s\r\n";
    public const string R_N_CONCMD_PC_5 = "déconnectée";
    public const string R_N_CONCMD_PC_6 = "Niveau: %s\r\n";
    public const string R_N_CONCMD_PC_7 = "Compte: %s\r\n";
    public const string R_N_CONCMD_RC = "Fichier Config ré-analysé\r\n";
}

public static class ConsoleListener
{
    public const string R_N_CONLIS_P = "Mot de passe: ";
    public const string R_N_CONLIS_P_1 = "\r\nTentative d'authentification. Veuillez patienter.\r\n";
    public const string R_N_CONLIS_P_2 = "Abandon";
    public const string R_N_CONLIS_L = "Bienvenue sur la console d'administration à distance WAAD.\r\n";
    public const string R_N_CONLIS_L_1 = "Veuillez vous authentifier avant de continuer. \r\n\r\n";
    public const string R_N_CONLIS_L_2 = "login: ";
    public const string R_N_CONLIS_A = "Échec de l'authentification.\r\n\r\n";
    public const string R_N_CONLIS_A_1 = "Utilisateur `%s` authentifié.\r\n\r\n";
    public const string R_N_CONLIS_A_2 = "Taper ? pour la liste des commandes, quit pour mettre fin à la session.\r\n";
    public const string R_N_CONLIS_I = "Affiche le message dans toutes les boîtes de dialogue du client.";
    public const string R_N_CONLIS_I_1 = "Bannissement du compte x pour une durée de y.";
    public const string R_N_CONLIS_I_2 = "Crée un compte.";
    public const string R_N_CONLIS_I_3 = "Ré-initialisation du mot de passe du compte.";
    public const string R_N_CONLIS_I_4 = "Sauvegarde du personnage dans la base de données.";
    public const string R_N_CONLIS_I_5 = "Annule un arrêt en attente.";
    public const string R_N_CONLIS_I_6 = "Donne des informations sur le temps d'exécution du serveur.";
    public const string R_N_CONLIS_I_7 = "Montre les GMs en ligne.";
    public const string R_N_CONLIS_I_8 = "Renvoie le joueur x pour la raison y";
    public const string R_N_CONLIS_I_9 = "Voir le message du jour (MOTD)";
    public const string R_N_CONLIS_I_10 = "Définir un nouveau message du jour (MOTD)";
    public const string R_N_CONLIS_I_11 = "Montre les joueurs en ligne.";
    public const string R_N_CONLIS_I_12 = "Montre les informations d'un joueur.";
    public const string R_N_CONLIS_I_13 = "Arrête le serveur avec un délai optionnel en secondes.";
    public const string R_N_CONLIS_I_14 = "Recharge le fichier de configuration.";
    public const string R_N_CONLIS_I_15 = "Dé-bannissement du compte x.";
    public const string R_N_CONLIS_I_16 = "Affiche le message dans tous les canaux du client.";
    public const string R_N_CONLIS_I_17 = "Chuchote un message à quelqu'un de la console.";
    public const string R_N_CONLIS_I_18 = "[!]Erreur ! '%s' utilise une syntaxe incorrecte. La bonne syntaxe est: '%s'.\r\n\r\n";
    public const string R_N_CONLIS_I_19 = "[!]Erreur ! La commande '%s' n'existe pas. Taper '?' ou 'help' pour obtenir la liste des commandes.\r\n\r\n";
}

public static class LogonCommClient
{
    public const string R_E_LOGCOMCLT = "LogonCommClient: Taille du paquet invalide.\n";
    public const string R_E_LOGCOMCLT_1 = "LogonCommClient: Réception d'un paquet inconnu: %u\n";
    public const string R_E_LOGCOMCLT_2 = "Abandon de la connexion due à la déconnexion du serveur de Logon.\n";
    public const string R_E_LOGCOMCLT_3 = "Échec de l'authentification !";
    public const string R_E_LOGCOMCLT_4 = "deflateInit: Échec.";
    public const string R_E_LOGCOMCLT_5 = "deflate: Échec.";
    public const string R_E_LOGCOMCLT_6 = "deflate: Échec. N'a pas mis fin au flux";
    public const string R_E_LOGCOMCLT_7 = "deflateEnd: Échec.";
    public const string R_N_LOGCOMCLT = "\n        >> Le serveur de Royaume(s) `%s` est enregistré sous l'id ";
    public const string R_N_LOGCOMCLT_1 = "A pris %u msec pour construire la liste de la cartographie des personnages pour le royaume %u";
    public const string R_D_LOGCOMCLT = ">> Latence du Serveur de Logon: %ums";
}

public static class LogonCommHandler
{
    public const string R_N_LOGCOMHAN = "\n >> Chargement des définitions des serveurs de Royaume(s) et de Logon... \n";
    public const string R_N_LOGCOMHAN_1 = " >> Tentative de connexion à tous les serveurs de Logon... \n";
    public const string R_N_LOGCOMHAN_2 = "	>> Connexion à `%s` sur `%s:%u`...";
    public const string R_N_LOGCOMHAN_3 = "        >> Authentification...\n";
    public const string R_N_LOGCOMHAN_4 = "        >> Résultat :";
    public const string R_N_LOGCOMHAN_5 = "        >> Enregistrement du serveur de Royaume(s)... ";
    public const string R_N_LOGCOMHAN_6 = "\n        >> Test ping: ";
    public const string R_N_LOGCOMHAN_7 = " >> La connexion du serveur de Royaume(s) avec l'id %u a été abandonnée de façon inattendue. Reconnexion au prochain passage.";
    public const string R_N_LOGCOMHAN_8 = " >> Récupération des informations pour le compte : `%s` (Requête %u).\n";
    public const string R_Y_LOGCOMHAN = " Connexion au serveur de Logon : Délais dépassé.\n";
    public const string R_Y_LOGCOMHAN_1 = " Connexion au serveur de Royaume(s) : Délais dépassé.\n";
    public const string R_E_LOGCOMHAN = " Échec de la connexion au serveur. Une nouvelle tentative sera faite ultérieurement.\n";
    public const string R_E_LOGCOMHAN_1 = " Échec.\n";
    public const string R_E_LOGCOMHAN_2 = "Le serveur de Royaume(s) avec l'id %u a perdu la connexion.";
    public const string R_E_LOGCOMHAN_3 = "La connexion au serveur de Royaume(s) avec l'id %u a été supprimée en raison du dépassement du délais du pong.";
    public const string R_E_LOGCOMHAN_4 = "\n   >> Aucun serveur de Royaume(s) trouvé. Ce serveur ne sera en ligne nulle part !\n";
}

public static class ClientManager
{
    public const string R_S_CLTMGR = "Interface créée";
    public const string R_D_CLTMGR = "Nouvel id de session max: %u";
    public const string R_D_CLTMGR_1 = "Allocation de la session %u pour l'id du compte %u";
}

public static class ClusterManager
{
    public const string R_S_CLUSMGR = "Allocation du serveur de 'Monde' %u à l'adresse:port %s:%u";
    public const string R_S_CLUSMGR_1 = "Allocation de l'instance %u sur la map %u pour le serveur %u";
    public const string R_W_CLUSMGR = "Suppression de l'instance %u sur la Map %u due à la déconnexion du serveur de 'Monde'";
    public const string R_W_CLUSMGR_1 = "Suppression de l'instance 'Prototype Map' %u due à la déconnexion du serveur de 'Monde'";
    public const string R_W_CLUSMGR_2 = "Suppression de la Map %u due à la déconnexion du serveur de 'Monde'";
    public const string R_W_CLUSMGR_3 = "Suppression du serveur de 'Monde' %u due à la fermeture du socket";
}

public static class WorkerServer
{
    public const string R_D_WORKMGR = "Attribution du prototype d'instance sur la Map %u pour le Worker %u";
    public const string R_D_WORKMGR_1 = "Ne trouve pas d'instance. Le prototype sera utilisé...";
    public const string R_D_WORKMGR_2 = "HandleChannelAction opcode, action %u non traitée";
    public const string R_D_WORKMGR_3 = "Réception de l'opcode ICMSG_CHANNEL_UPDATE ; type %u mis à jour";
    public const string R_D_WORKMGR_4 = "Réception de l'opcode ICMSG_CHANNEL_UPDATE depuis le guid %u du joueur";
    public const string R_D_WORKMGR_5 = "HandleChannelUpdate opcode, type %u mise à jour non traitée";
    public const string R_D_WORKMGR_6 = "Quitte le canal de communication %u for %s";
    public const string R_D_WORKMGR_7 = "Rejoint le canal de communication %u for %s";
    public const string R_D_WORKMGR_8 = "HandleChannelUpdate opcode, UPDATE_CHANNELS_ON_ZONE_CHANGE";
    public const string R_S_WORKMGR = "Connexion sur le serveur de 'Monde' %u avec succès pour le joueur %u";
    public const string R_W_WORKMGR = "Il n'existe pas de session pour la connexion du joueur";
    public const string R_E_WORKMGR = "Échec de la connexion sur le serveur de 'Monde' %u pour le joueur %u";
    public const string R_E_WORKMGR_1 = "La session n'existe pas. Reporter aux développeurs";
    public const string R_E_WORKMGR_2 = "Paquet %u non traité.\n";
    public const string R_E_WORKMGR_3 = "Ne peut pas créer le canal de communication %s !";
    public const string R_N_WORKMGR = "Téléportation intra-server";
    public const string R_N_WORKMGR_1 = "Téléportation inter-server";
}

public static class WorkerServerSocket
{
    public const string R_N_WORKSRVSOC = "Réponse à l'authentification. Le serveur est %s ; build %u";
}

public static class Master
{
    public const string R_E_MASTER = "Fatal: Le nom du répertoire des DBCs est trop long ! (%u caractères max)";
    public const string R_E_MASTER_1 = "       Retour au nom par défaut 'dbc'";
    public const string R_E_MASTER_2 = "Un ou plusieurs fichiers'DBC' sont manquants";
    public const string R_E_MASTER_3 = "Ils sont absolument nécessaires pour le bon fonctionnement du serveur";
    public const string R_E_MASTER_4 = "Le serveur ne démarrera pas sans eux.";
    public const string R_E_MASTER_5 = "Ne peux pas ouvrir un des socket.";
    public const string R_E_MASTER_6 = "Un ou plusieurs paramètres sont manquants pour la directive Database.Realm";
    public const string R_E_MASTER_7 = "Échec de l'initialisation de la base de données principale. Fermeture du Core";
    public const string R_E_MASTER_8 = "Un ou plusieurs paramètres sont manquants pour la directive Database.Character";
    public const string R_N_MASTER = "Vérification du fichier de configuration : %s";
    public const string R_N_MASTER_1 = "Appuyer sur les touches <Ctrl + C> pour permettre l'arrêt du serveur en toute sécurité.";
    public const string R_N_MASTER_2 = "Chargement des fichiers de configuration...";
    public const string R_N_MASTER_3 = "Chargement des fichiers DBC...";
    public const string R_N_MASTER_4 = "Ouverture du port pour le client...";
    public const string R_N_MASTER_5 = "Ouverture du port pour le serveur...";
    public const string R_N_MASTER_6 = "Initialisée à %s";
    public const string R_N_MASTER_7 = "Fermeture du LogonCommHandler";
    public const string R_N_MASTER_8 = "Suppression du sous-système réseau...";
    public const string R_N_MASTER_9 = "Fermeture du port client...";
    public const string R_N_MASTER_10 = "Fermeture du port serveur...";
    public const string R_N_MASTER_11 = "Fermeture du sous-système réseau...";
    public const string R_N_MASTER_12 = "Fichiers DBC déchargés...";
    public const string R_N_MASTER_13 = "Fermeture des connexions...";
    public const string R_N_MASTER_14 = "Fermeture des bases de données terminée.";
    public const string R_N_MASTER_15 = "Fermeture du thread pool";
    public const string R_S_MASTER = "Chargement de la configuration avec succès.";
    public const string R_S_MASTER_1 = "Générateurs de nombres aléatoires initialisés.";
    public const string R_S_MASTER_2 = "Connexions établies...";
    public const string R_S_MASTER_3 = "Fichiers DBC chargés...";
    public const string R_S_MASTER_4 = "Sous-système réseau démarré.";
    public const string R_S_MASTER_5 = "Prêt à recevoir les connexions. Temps de démarrage: %ums\n";
    public const string R_S_MASTER_6 = "Fermeture terminée.";
    public const string R_W_MASTER = "Le chargement de la configuration a rencontré une ou plusieurs erreurs.";
    public const string R_W_MASTER_1 = "Directive die interceptée. Vous devez effacer ou commenter les directives die et die2 de votre fichier de configuration avant de continuer.";
    public const string R_W_MASTER_2 = "Waad est exécuté avec des privilèges de super-utilisateur (root)";
    public const string R_W_MASTER_3 = "Ce n'est pas nécessaire, et peut-être un possible risque de sécurité.";
    public const string R_W_MASTER_4 = "Appuyer sur les touches <Ctrl + C> et relancez le core WAAD avec un compte utilisateur sans privilèges.";
    public const string R_W_MASTER_5 = "Directive die interceptée: %s";
    public const string R_N_MASTER_CH = "En attente de fermeture de toutes les requêtes de la base de données";
    public const string R_N_MASTER_CH_1 = "Toutes les opérations de base de données en attente ont été effacées.";
    public const string R_N_MASTER_CH_2 = "Données sauvegardées.";
    public const string R_N_MASTER_CH_3 = "Une exception a été provoquée pendant l'enregistrement des données.";
    public const string R_N_MASTER_CH_4 = "Fermeture.";
}
