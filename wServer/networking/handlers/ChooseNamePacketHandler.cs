﻿using db;
using MySql.Data.MySqlClient;
using wServer.networking.cliPackets;
using wServer.networking.svrPackets;
using wServer.realm.entities;

namespace wServer.networking.handlers
{
    internal class ChooseNamePacketHandler : PacketHandlerBase<ChooseNamePacket>
    {
        public override PacketID ID
        {
            get { return PacketID.ChooseName; }
        }

        protected override void HandlePacket(Client client, ChooseNamePacket packet)
        {
            if (string.IsNullOrEmpty(packet.Name) ||
                packet.Name.Length > 16)
            {
                client.SendPacket(new NameResultPacket
                {
                    Success = false,
                    Message = "Invalid name"
                });
            }
            client.Manager.Data.AddPendingAction(db =>
            {
                MySqlCommand cmd = db.CreateQuery();
                cmd.CommandText = "SELECT COUNT(name) FROM accounts WHERE name=@name;";
                cmd.Parameters.AddWithValue("@name", packet.Name);
                if ((int)(long)cmd.ExecuteScalar() > 0)
                    client.SendPacket(new NameResultPacket
                    {
                        Success = false,
                        Message = "Duplicated name"
                    });
                else if (packet.Name.Length < 3)
                    client.SendPacket(new NameResultPacket
                    {
                        Success = false,
                        Message = "Name too short, minimum 3 letters"
                    });
                else if (packet.Name.Contains(" "))
                    client.SendPacket(new NameResultPacket
                    {
                        Success = false,
                        Message = "Cannot have a space in username"
                    });
                else
                {
                    db.ReadStats(client.Account);
                    if (client.Account.NameChosen && client.Account.Credits < 1000)
                        client.SendPacket(new NameResultPacket
                        {
                            Success = false,
                            Message = "Not enough credits"
                        });
                    else if (!client.Account.NameChosen)
                    {
                        cmd = db.CreateQuery();
                        cmd.CommandText = "UPDATE accounts SET name=@name, namechosen=TRUE WHERE id=@accId;";
                        cmd.Parameters.AddWithValue("@accId", client.Account.AccountId);
                        cmd.Parameters.AddWithValue("@name", packet.Name);
                        if (cmd.ExecuteNonQuery() > 0)
                        {
                            client.Account.Name = packet.Name;
                            client.Manager.Logic.AddPendingAction(t => Handle(client.Player));
                            client.SendPacket(new NameResultPacket
                            {
                                Success = true,
                                Message = ""
                            });
                        }
                        else
                            client.SendPacket(new NameResultPacket
                            {
                                Success = false,
                                Message = "Internal Error"
                            });
                    }
                    else
                    {
                        cmd = db.CreateQuery();
                        cmd.CommandText = "UPDATE accounts SET name=@name, namechosen=TRUE WHERE id=@accId;";
                        cmd.Parameters.AddWithValue("@accId", client.Account.AccountId);
                        cmd.Parameters.AddWithValue("@name", packet.Name);
                        if (cmd.ExecuteNonQuery() > 0)
                        {
                            client.Account.Credits = db.UpdateCredit(client.Account, -1000);
                            client.Account.Name = packet.Name;
                            client.Manager.Logic.AddPendingAction(t => Handle(client.Player));
                            client.SendPacket(new NameResultPacket
                            {
                                Success = true,
                                Message = ""
                            });
                        }
                        else
                            client.SendPacket(new NameResultPacket
                            {
                                Success = false,
                                Message = "Internal Error"
                            });
                    }
                }
            });
        }

        private void Handle(Player player)
        {
            player.Credits = player.Client.Account.Credits;
            player.Name = player.Client.Account.Name;
            player.NameChosen = true;
            player.UpdateCount++;
        }
    }
}