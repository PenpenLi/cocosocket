﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace cocosocket4unity
{
    public abstract class KcpOnUdp : Output
    {
      protected UdpClient client;
      protected Kcp kcp;
      protected IPEndPoint serverAddr;
      protected Object LOCK = new Object();//加锁访问收到的数据
      protected LinkedList<byte[]> received;
      protected int nodelay;
      protected int interval = Kcp.IKCP_INTERVAL;
      protected int resend;
      protected int nc;
      protected int sndwnd = Kcp.IKCP_WND_SND;
      protected int rcvwnd = Kcp.IKCP_WND_RCV;
      protected int mtu = Kcp.IKCP_MTU_DEF;
      protected volatile bool needUpdate;
      public KcpOnUdp(int port)
      {
         client = new UdpClient(port); 
         kcp = new Kcp(121106, this,null);
         this.received = new LinkedList<byte[]>();
      }
      /// <summary>
      /// 连接到地址
      /// </summary>
      public void Connect(string host,int port)
      { 
          serverAddr=new IPEndPoint(IPAddress.Parse(host),port);
          //mode setting
          kcp.NoDelay(nodelay, interval, resend, nc);
          kcp.WndSize(sndwnd, rcvwnd);
          kcp.SetMtu(mtu);
          this.client.Connect(serverAddr);
          client.BeginReceive(Received, client);
      }
      public override void output(ByteBuf msg, Kcp kcp, Object user) 
      {
          this.client.Send(msg.GetRaw(),msg.ReadableBytes());
      }
      private void Received(IAsyncResult ar)
      {
          UdpClient client = (UdpClient)ar.AsyncState;
          byte[] data=client.EndReceive(ar, ref this.serverAddr);
          lock(LOCK)
          {
            this.received.AddLast(data);
            this.needUpdate = true;
          }
          client.BeginReceive(Received, ar.AsyncState);
      }
      /**
  * update one kcp
  *
  * @param addr
  * @param kcp
  */
  public void Update()
  {
    //input
      lock (LOCK)
      {  
        while (this.received.Count>0)
        {
        byte[] dp = this.received.First.Value;
        kcp.Input(new ByteBuf(dp));
        this.received.RemoveFirst();
        }
      }
    //receive
    int len;
    while ((len = kcp.PeekSize()) > 0)
    {
      ByteBuf bb = new ByteBuf(len);
      int n = kcp.Receive(bb);
      if (n > 0)
      {
        this.HandleReceive(bb);
      }
    }
    //update kcp status
    int cur = (int)DateTime.Now.Ticks;
    if (this.needUpdate|| cur >= kcp.GetNextUpdate())
    {
      kcp.Update(cur);
      kcp.SetNextUpdate(kcp.Check(cur));
      this.needUpdate = false;
    }
  }
        /**
         * 处理收到的消息
         */ 
  protected abstract void HandleReceive(ByteBuf bb);
      /**
       * fastest: ikcp_nodelay(kcp, 1, 20, 2, 1) nodelay: 0:disable(default),
       * 1:enable interval: internal update timer interval in millisec, default is
       * 100ms resend: 0:disable fast resend(default), 1:enable fast resend nc:
       * 0:normal congestion control(default), 1:disable congestion control
       *
       * @param nodelay
       * @param interval
       * @param resend
       * @param nc
       */
      public void NoDelay(int nodelay, int interval, int resend, int nc)
      {
          this.nodelay = nodelay;
          this.interval = interval;
          this.resend = resend;
          this.nc = nc;
      }

      /**
       * set maximum window size: sndwnd=32, rcvwnd=32 by default
       *
       * @param sndwnd
       * @param rcvwnd
       */
      public void WndSize(int sndwnd, int rcvwnd)
      {
          this.sndwnd = sndwnd;
          this.rcvwnd = rcvwnd;
      }

      /**
       * change MTU size, default is 1400
       *
       * @param mtu
       */
      public void SetMtu(int mtu)
      {
          this.mtu = mtu;
      }
    }
}
