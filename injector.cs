using System;
using System.Windows.Forms;
using System.Threading;
using System.Security.Principal;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Text;
using System.IO;

namespace Sniffer
{
    public class RemoteChannel
    {
        Form1 form;
        NamedPipeServerStream pipeDataStream;
        NamedPipeServerStream pipeCmdStream;
        
        const int bufferSize = 8192 * 160;

        public RemoteChannel(Form1 f)
        {
            form = f;
            pipeDataStream = new NamedPipeServerStream
                ("injector_awesome_2000", PipeDirection.InOut, 100,
                PipeTransmissionMode.Message, PipeOptions.Asynchronous, bufferSize, bufferSize);

            pipeCmdStream = new NamedPipeServerStream
                 ("injector_awesome_2000_cmd", PipeDirection.InOut, 100,
                 PipeTransmissionMode.Message, PipeOptions.Asynchronous, bufferSize, bufferSize);
        }

        public void SendPackage(MessagePackage msg)
        {
            if (!pipeCmdStream.IsConnected)
                return;

            string cmd = "s|" + msg.socketHandle.ToString() + "|";
            byte[] header = Encoding.ASCII.GetBytes(cmd);
            MemoryStream ms = new MemoryStream(header.Length + msg.data.Length);
            BinaryWriter writer = new BinaryWriter(ms);
            writer.Write(header);
            writer.Write(msg.data);

            pipeCmdStream.Write(ms.GetBuffer(), 0, (int)ms.Length);

            pipeCmdStream.Flush();

            BinaryReader br = new BinaryReader(pipeCmdStream);
            var code = br.ReadUInt32();

            if (code != 0)
            {
                throw new System.ComponentModel.Win32Exception((int)code);
            }
        }

        public void SendStartRecord()
        {
            if (!pipeCmdStream.IsConnected)
                return;

            string cmd = "r";
            byte[] data = Encoding.ASCII.GetBytes(cmd);
            pipeCmdStream.Write(data, 0, data.Length);
            pipeCmdStream.Flush();
        }

        public void SendPauseRecord()
        {
            if (!pipeCmdStream.IsConnected)
                return;

            string cmd = "p";
            byte[] data = Encoding.ASCII.GetBytes(cmd);
            pipeCmdStream.Write(data, 0, data.Length);
            pipeCmdStream.Flush();
        }

        public void Close()
        {
            if (pipeDataStream.IsConnected)
            {
                pipeDataStream.Disconnect();
            }
            if (pipeCmdStream.IsConnected)
            {
                pipeCmdStream.Disconnect();
            }

            pipeDataStream.Close();
            pipeCmdStream.Close();
        }

        public void Start()
        {
            WaitForConnection();

            Thread thread = new Thread(DoWork);
            thread.Start();
        }

        void WaitForConnection()
        {
            this.pipeDataStream.WaitForConnection();
            this.pipeCmdStream.WaitForConnection();
        }


        public void DoWork()
        {
            byte[] buffer = new byte[bufferSize];

            while (this.pipeDataStream.IsConnected)
            {
                int readed = this.pipeDataStream.Read(buffer, 0, bufferSize);
                if (readed <= 0)
                {
                    continue;
                }

                int ptr = 0;
                while (true)
                {
                    StringBuilder strBuilder = new StringBuilder(20);
                    MessagePackage package = new MessagePackage();
                    int realLength = 0;


                    for (int i = ptr; i < readed; i++)
                    {
                        if (buffer[i] == '|')
                        {
                            realLength = Int32.Parse(strBuilder.ToString());
                            strBuilder.Clear();
                            ptr += (i + 1);
                            break;
                        }
                        else
                        {
                            var s = (char)buffer[i];
                            strBuilder.Append(s);
                        }
                    }

                    if (buffer[ptr] == 's')
                    {
                        package.type = "send";
                    }
                    else if (buffer[ptr] == 'r')
                    {
                        package.type = "recv";
                    }

                    // extract addr
                    for (int i = 2 + ptr; i < readed; i++)
                    {
                        if (buffer[i] != '|')
                        {
                            strBuilder.Append((char)buffer[i]);
                        }
                        else
                        {
                            ptr = i;
                            break;
                        }
                    }
                    package.from = strBuilder.ToString();

                    // extract socket
                    strBuilder.Clear();
                    for (int i = ptr + 1; i < readed; i++)
                    {
                        if (buffer[i] != '|')
                        {
                            strBuilder.Append((char)buffer[i]);
                        }
                        else
                        {
                            ptr = i;
                            break;
                        }
                    }

                    package.socketHandle = Int64.Parse(strBuilder.ToString());

                    package.data = new byte[realLength];

                    int counter = 0;
                    bool haveAnotherPackage = false;
                    for (int i = ptr + 1; i < readed; i++)
                    {
                        ptr = i;
                        // another package starting
                        if (counter + 1 > realLength)
                        {
                            haveAnotherPackage = true;
                            break;
                        }
                        package.data[counter] = buffer[i];
                        counter++;
                    }

                    this.form.GetListView().Invoke((MethodInvoker)(() =>
                    {
                        this.form.AddPackage(package);
                    }));

                    if (!haveAnotherPackage)
                    {
                        break;
                    }
                }
            }
        }
    }
}