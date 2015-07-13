/**
 * varint
 */ 
using System;
namespace cocosocket4unity
{
	public class Varint32HeaderProtocol : Protocal
	{
		
		private  const int STATUS_HEADER = 0;//读头
		private  const int STATUS_CONTENT = 1;//读内容
		
		private  byte[] header;
		private int index;
		private int status;
		private int len;
		private ByteBuf incompleteframe;//尚未完成的帧

		public Varint32HeaderProtocol ()
		{
			header = new byte[5];
		}
		/**
		 * 分帧逻辑
		 * 
		 **/ 
		public ByteBuf TranslateFrame(ByteBuf src)
		{
			while (src.ReadableBytes() > 0)
			{
				switch (status)
				{
				case STATUS_HEADER:
					for (; index < header.Length; index++)
					{
						if (!(src.ReadableBytes() > 0))
						{
							break;
						}
						header[index] = src.ReadByte();
						if (header[index] >= 0)
						{
							int length = 0;
							length = CodedInputStream.newInstance(header, 0, index + 1).readRawVarint32();
							if (length < 0)
							{
							    return null;
							}
							len = length;
							status = STATUS_CONTENT;
							int headerLen = CodedOutputStream.computeRawVarint32Size(len);
							incompleteframe = new ByteBuf(len + headerLen);
							CodedOutputStream headerOut = CodedOutputStream.newInstance(incompleteframe, headerLen);
							headerOut.writeRawVarint32(len);
							headerOut.flush();
							break;
						}
					}
					break;
				case STATUS_CONTENT:
					int l = Math.Min(src.ReadableBytes(), incompleteframe.WritableBytes());
					if (l > 0)
					{
						incompleteframe.WriteBytes(src, l);
					}
					if (incompleteframe.WritableBytes() <= 0)
					{
						status = STATUS_HEADER;
						index = 0;
						return incompleteframe;
					}
					break;
				}
			}
			return null;
		}
	}
}

