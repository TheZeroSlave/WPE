using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Sniffer
{
    public class SendingList
    {
        Dictionary<string, MessagePackage> messages = new Dictionary<string, MessagePackage>();
        System.Windows.Forms.CheckedListBox listBox;

        public delegate void ItemCountChanged(int count);
        public event ItemCountChanged OnItemCountChanged;

        public SendingList(System.Windows.Forms.CheckedListBox lb)
        {
            listBox = lb;
        }

        public int Length
        {
            get { return messages.Count; }
        }

        public string GenerateUniqName()
        {
            string defaultName = "sendPackage_" + this.Length;
            if (!HasName(defaultName))
                return defaultName;
            
            var rand = new Random();
            while (true)
            {
                string newName = defaultName + "_" + rand.Next().ToString();
                if (!HasName(defaultName))
                    return newName;
            }

            return "justNotPossible";
        }

        public void Remove(int index)
        {
            var item = listBox.Items[index];
            messages.Remove(item.ToString());
            listBox.Items.RemoveAt(index);

            OnItemCountChanged?.Invoke(messages.Count);
        }

        public bool HasName(string name)
        {
            return messages.ContainsKey(name);
        }

        public void AddLast(string nm, MessagePackage m)
        {
            messages.Add(nm, m);
            listBox.Items.Add(nm, true);
            if (listBox.Items.Count == 1)
            {
                listBox.SelectedIndex = 0;
            }
            OnItemCountChanged?.Invoke(messages.Count);
        }

        public MessagePackage[] SelectedPackages()
        {
            MessagePackage[] msgs = new MessagePackage[listBox.CheckedItems.Count];
            for (int i = 0; i < listBox.CheckedItems.Count; i++)
            {
                var msg = messages[listBox.CheckedItems[i].ToString()];
                msgs[i] = msg;
            }
            return msgs;
        }

        public void SetSocketHandle(long socketHandle)
        {
            foreach(var p in messages)
            {
                messages[p.Key].socketHandle = socketHandle;
            }
        }

        public void Rename(int index, string newName)
        {
            var item = listBox.Items[index];
            var mp = messages[item.ToString()];

            messages.Remove(item.ToString());
            messages.Add(newName, mp);

            listBox.Items.RemoveAt(index);
            listBox.Items.Insert(index, newName);
        }

        public MessagePackage GetPackage(int index)
        {
            var item = listBox.Items[index];
            return messages[item.ToString()];
        }

        public void SaveAllToFile(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Create))
            {
                var stream = new BinaryWriter(fs);

                stream.Write(listBox.Items.Count);


                int i = 0;
                foreach (var item in listBox.Items)
                {
                    var packageName = (string)item;
                    var isChecked = listBox.GetItemChecked(i);
                    var msg = messages[packageName];
                    stream.Write(isChecked);
                    stream.Write(packageName);
                    stream.Write(msg.data.Length);
                    stream.Write(msg.data);
                    stream.Write(msg.socketHandle);
                }
            }
        }

        public void LoadFromFile(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open))
            {
                var stream = new BinaryReader(fs);

                var count = stream.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    var checkedItem = stream.ReadBoolean();
                    var packageName = stream.ReadString();
                    var countBytes = stream.ReadInt32();
                    var bytes = stream.ReadBytes(countBytes);
                    var handle = stream.ReadInt32();

                    var pkg = new MessagePackage(bytes, handle);
                    AddLast(packageName, pkg);
                    listBox.SetItemChecked(i, checkedItem);
                }
            }
        }
    }
}
