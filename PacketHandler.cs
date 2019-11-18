using System;
using System.Collections.Generic;
using System.Text;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;

namespace Tela
{
  public delegate void OnPacketReceive(NetState state, PacketReader pvSrc);
  public class PacketHandler
  {
    public int ID { get; private set; }
    public int Length { get; private set; }
    public bool InGame { get; private set; }
    public OnPacketReceive OnReceive { get; private set; }

    public PacketHandler(int id, int length, bool ingame, OnPacketReceive onReceive)
    {
      ID = id;
      Length = length;
      InGame = ingame;
      OnReceive = onReceive;
    }
  }
}
