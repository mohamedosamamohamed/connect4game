﻿using ShardClassLibrary;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace SimpleServer.ClassLib
{
	public class ClientHandler
	{
		public TcpClient Socket { get; set; }
		public string UserName { get; set; }
		public string Counter { get; set; }
		public int CurrentRoomNumber { get; set; }
		public bool IsOwner { get; set; }
		public bool IsPlayer { get; set; }
		public Color DiskColor { get; set; }

		NetworkStream networkStream;
		BinaryWriter bWriter;
		BinaryReader bReader;

		public ClientHandler(TcpClient soc)
		{
			Socket = soc;
			Thread clientThread = new Thread(this.AcceptRequests);
			clientThread.Start();
		}

		public void AcceptRequests()
		{
			while (true)
			{
				networkStream = Socket.GetStream();
				if (networkStream.DataAvailable)
				{
					bReader = new BinaryReader(networkStream);
					string reqRes = bReader.ReadString();
					int status = int.Parse(reqRes.Split(',')[0]);
					switch (status)
					{
						case 0:
							LogInUser(reqRes.Split(',')[2]);
							break;
						case 2:
							RedirectToWaitingRoom();
							break;
						case 3:
							SaveNewRoomData(reqRes.Split(',')[2]);
							break;
						case 4:
							SendRoomData(int.Parse(reqRes.Split(',')[2]));
							UpdateOnlineMembersToOthers(int.Parse(reqRes.Split(',')[2]));
							AddNewGuestToRoom(int.Parse(reqRes.Split(',')[2]));
							break;
						case 5:
							AddNewSpectatorToRoom(int.Parse(reqRes.Split(',')[2]));
							SendRoomDataToSpectator(int.Parse(reqRes.Split(',')[2]));
							UpdateOnlineSpectatorToOthers(int.Parse(reqRes.Split(',')[2]));
							break;
						case 6:
							SendGameStartRequestForCounter(
								reqRes.Split(',')[2],
								int.Parse(reqRes.Split(',')[3])
								);
							break;
						case 7:
						case -7:
							ReceiveCounterResponse(
								int.Parse(reqRes.Split(',')[0]),
								int.Parse(reqRes.Split(',')[2])
								);
							break;
							
					}
				}
			}
		}

		private void ReceiveCounterResponse(int header, int roomIdx)
		{
			foreach (ClientHandler cln in DataLayer.Clients)
			{
				if (cln.IsPlayer == true && cln.UserName == this.UserName && cln.CurrentRoomNumber == roomIdx)
				{
					bWriter = new BinaryWriter(cln.Socket.GetStream());
					if (header == 7)
					{
						bWriter.Write($"77,Your request accepted,{this.UserName}");
					}
					else
					{
						bWriter.Write($"-77,Your request refused,{this.UserName}");
					}
				}
			}
		}

		private void PickColor(string strColor)
		{
			switch (strColor)
			{
				case "Red":
					this.DiskColor = Color.Red;
					break;
				case "Yellow":
					this.DiskColor = Color.Yellow;
					break;
				case "Blue":
					this.DiskColor = Color.Blue;
					break;
			}
		}

		private void SendGameStartRequestForCounter(string strColor, int roomIdx)
		{
			PickColor(strColor);
			ClientHandler counterClient;
			foreach (ClientHandler cln in DataLayer.Clients)
			{
				if (cln.IsPlayer == true && cln.UserName != this.UserName && cln.CurrentRoomNumber == roomIdx)
				{
					bWriter = new BinaryWriter(cln.Socket.GetStream());
					bWriter.Write($"66,redirect counter start game request,{this.UserName}");
				}
			}

			//bWriter = new BinaryWriter(counterClient.Socket.GetStream());
			//bWriter.Write($"66,redirect counter start game request,{this.UserName}");

		}

		private void LogInUser(string userName)
		{
			var matches = DataLayer.Users.Where(u => u.UserName == userName).ToList();
			bWriter = new BinaryWriter(networkStream);
			if (matches.Count > 0)
			{
				var conntectedMatches = DataLayer.ConnectedUsers.Where(u => u.UserName == userName).ToList();
				if (conntectedMatches.Count > 0)
				{
					bWriter.Write("-1,This User is already connected to server.");
				}
				else
				{
					this.UserName = userName;
					DataLayer.ConnectedUsers.AddRange(matches);
					if (DataLayer.Rooms.Count > 0)
					{
						bWriter.Write($"11,{Views.RoomsList}");
						BinaryFormatter bf = new BinaryFormatter();
						bf.Serialize(networkStream, DataLayer.Rooms);
					}
					else
					{
						bWriter.Write($"1,{Views.RoomsList}");
					}
				}
			}
			else
			{
				bWriter.Write("-1,Can't find username.");
			}
		}

		private void RedirectToWaitingRoom()
		{
			bWriter = new BinaryWriter(networkStream);
			bWriter.Write($"1,{Views.WaitingRoom}");
		}

		private void SaveNewRoomData(string roomData)
		{
			string[] splitedData = roomData.Split(';');
			int idx = int.Parse(splitedData[0]);
			this.CurrentRoomNumber = idx;
			User roomOwner = new User { UserName = splitedData[2] };
			DataLayer.Rooms.Add(
				new Room
				{
					Index = idx,
					RoomName = splitedData[1],
					RoomBoardSize = splitedData[3],
					RoomOwnerDiskColor = splitedData[4],
					Players = new List<User>(),
					Spectators = new List<User>(),
					RoomOwner = roomOwner
				});
			DataLayer.Rooms[idx].Players.Add(roomOwner);
			this.IsPlayer = true;
			this.IsOwner = true;
			PickColor(splitedData[4]);

			/* Check if there are players connected to send new data to them */
			if (DataLayer.ConnectedUsers.Count > 1)
			{
				SendNewCreatedRoomsDataToClients();
			}
		}

		private void SendNewCreatedRoomsDataToClients()
		{
			for (int i = 0; i < DataLayer.Clients.Count; i++)
			{
				NetworkStream clientNetWorkStream = DataLayer.Clients[i].Socket.GetStream();
				if (clientNetWorkStream != networkStream)
				{
					bWriter = new BinaryWriter(clientNetWorkStream);
					bWriter.Write($"111,new rooms data");

					BinaryFormatter clinetBF = new BinaryFormatter();
					clinetBF.Serialize(clientNetWorkStream, DataLayer.Rooms);
				}
			}
		}

		private void SendRoomData(int roomIdx)
		{
			// Get Room
			Room matchRoom = DataLayer.Rooms[roomIdx];

			// SEND Msg Header
			bWriter = new BinaryWriter(networkStream);
			bWriter.Write("44,Accept");

			// SEND Room Object
			BinaryFormatter bf = new BinaryFormatter();
			bf.Serialize(networkStream, matchRoom);
		}

		private void SendRoomDataToSpectator(int roomIdx)
		{
			// Get Room
			Room matchRoom = DataLayer.Rooms[roomIdx];

			// SEND Msg Header
			bWriter = new BinaryWriter(networkStream);
			bWriter.Write("55,Accept");

			// SEND Room Object
			BinaryFormatter bf = new BinaryFormatter();
			bf.Serialize(networkStream, matchRoom);
		}

		private void AddNewGuestToRoom(int roomIdx)
		{
			this.CurrentRoomNumber = roomIdx;
			DataLayer.Rooms[roomIdx].Players.Add(
					new User { UserName = this.UserName }
				);
			this.IsPlayer = true;
		}

		private void AddNewSpectatorToRoom(int roomIdx)
		{
			this.CurrentRoomNumber = roomIdx;
			DataLayer.Rooms[roomIdx].Spectators.Add(
					new User { UserName = this.UserName }
				);
		}

		private void UpdateOnlineMembersToOthers(int roomIdx)
		{
			List<ClientHandler> roomPlayers = DataLayer.Clients.Where(c => c.CurrentRoomNumber == roomIdx).ToList();
			List<string> sendPlayers = new List<string>();

			for (int i = 0; i < roomPlayers.Count; i++)
			{
				sendPlayers.Add(roomPlayers[i].UserName);
			}

			for (int i = 0; i < roomPlayers.Count; i++)
			{

				if (roomPlayers[i].UserName != this.UserName)
				{
					NetworkStream clientNetWorkStream = roomPlayers[i].Socket.GetStream();
					bWriter = new BinaryWriter(clientNetWorkStream);
					bWriter.Write($"444,update room members");

					BinaryFormatter clinetBF = new BinaryFormatter();
					clinetBF.Serialize(clientNetWorkStream, sendPlayers);
				}
			}
		}

		private void UpdateOnlineSpectatorToOthers(int roomIdx)
		{
			List<ClientHandler> roomPlayers = DataLayer.Clients.Where(c => c.CurrentRoomNumber == roomIdx).ToList();
			List<ClientHandler> filterdSpecs = new List<ClientHandler>();

			for (int i = 0; i < roomPlayers.Count; i++)
			{
				for (int j = 0; j < DataLayer.Rooms[roomIdx].Spectators.Count; j++)
				{
					if (DataLayer.Rooms[roomIdx].Spectators[j].UserName == roomPlayers[i].UserName)
					{
						filterdSpecs.Add(roomPlayers[i]);
					}
				}
			}
			List<string> sendSpectators = new List<string>();

			for (int i = 0; i < filterdSpecs.Count; i++)
			{
				sendSpectators.Add(filterdSpecs[i].UserName);
			}

			for (int i = 0; i < filterdSpecs.Count; i++)
			{
				for (int j = 0; j < roomPlayers.Count; j++)
				{
					if (filterdSpecs[i].UserName != roomPlayers[i].UserName)
					{
						NetworkStream clientNetWorkStream = roomPlayers[i].Socket.GetStream();
						bWriter = new BinaryWriter(clientNetWorkStream);
						bWriter.Write($"555,update room members");
						BinaryFormatter clinetBF = new BinaryFormatter();
						clinetBF.Serialize(clientNetWorkStream, sendSpectators);
					}
				}
			}
		}
	}
}
