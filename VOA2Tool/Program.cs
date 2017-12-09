using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VOA2Tool
{
    class Program
    {
        const string MAGIC = "PACK_FILE001";
        const string ORIG_KEY = "aC6Bjma36a";
        const string STEAM_KEY = "6a4Bjab351";
        static Blowfish blowfish = new Blowfish(Encoding.ASCII.GetBytes("default"));

        static void Main(string[] args)
        {
            if (args.Length < 3 || args[0] != "--key" || (args[1] != "orig" && args[1] != "steam") || (args[2] != "--unpack" && args[2] != "--repack"))
                PrintUsage();

            blowfish = new Blowfish(Encoding.ASCII.GetBytes(args[1] == "orig" ? ORIG_KEY : STEAM_KEY));

            if (args[2] == "--unpack")
            {
                if (args.Length == 5)
                    pck_unpack(args[3], args[4]);
                else
                    pck_unpack(args[3]);
            }

            if (args[2] == "--repack")
            {
                if (args.Length == 5)
                    pck_pack(args[3], args[4]);
                else
                    pck_pack(args[3]);
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("VOA2Tool v1.0, code by sabpprook");
            Console.WriteLine("");
            Console.WriteLine("VOA2Tool --key [orig|steam] --unpack <pck_file> [default = \\out]");
            Console.WriteLine("VOA2Tool --key [orig|steam] --repack <pack_folder> [default = out.pck]");
            Console.WriteLine("");
            Environment.Exit(0);
        }

        static void pck_unpack(string pck_file, string out_folder = "out")
        {
            if (Directory.Exists(out_folder)) Directory.Delete(out_folder, true);
            Directory.CreateDirectory(out_folder);

            BinaryReader pck = new BinaryReader(File.OpenRead(pck_file));
            if (Encoding.ASCII.GetString(pck.ReadBytes(12)) != MAGIC) return;

            uint pck_file_count = pck.ReadUInt32();
            uint pck_info_length = pck.ReadUInt32();
            byte[] pck_info_byte = pck.ReadBytes((int)pck_info_length);

            blowfish.Decrypt_LE(pck_info_byte);

            BinaryReader pck_info = new BinaryReader(new MemoryStream(pck_info_byte));

            for (int i = 0; i < pck_file_count; i++)
            {
                uint data_len_orig = pck_info.ReadUInt32();
                string data_name = GetInfoFileString(pck_info);
                uint data_len_padded = pck_info.ReadUInt32();

                if (pck.ReadByte() == 0x00)
                    continue;

                Console.WriteLine($"-- {data_name}");

                byte[] data_buff = pck.ReadBytes((int)data_len_padded);
                blowfish.Decrypt_LE(data_buff);

                FileStream fs = File.Create($"{out_folder}\\{data_name}");
                fs.Write(data_buff, 0, (int)data_len_orig);
                fs.Close();
            }

            pck_info.Close();
            pck.Close();
        }

        static void pck_pack(string pack_folder, string out_file = "out.pck")
        {
            string[] data_list = Directory.GetFiles(pack_folder);
            if (data_list.Length == 0) return;

            MemoryStream ms_info = new MemoryStream();
            MemoryStream ms_pck = new MemoryStream();

            foreach (var data in data_list)
            {
                FileInfo fi = new FileInfo(data);
                byte[] data_len_orig = BitConverter.GetBytes((uint)fi.Length);
                byte[] data_name = Encoding.ASCII.GetBytes(fi.Name);
                byte[] data_len_padded = BitConverter.GetBytes((uint)fi.Length + GetPaddedLength((uint)fi.Length));

                Console.WriteLine($"-- {Encoding.ASCII.GetString(data_name)}");

                ms_info.Write(data_len_orig, 0, data_len_orig.Length);
                ms_info.Write(data_name, 0, data_name.Length);
                ms_info.WriteByte(0);
                ms_info.Write(data_len_padded, 0, data_len_padded.Length);

                ms_pck.WriteByte(0x02);
                byte[] data_buff = File.ReadAllBytes(data);
                data_buff = GetPaddedBuffer(data_buff);
                blowfish.Encrypt_LE(data_buff);
                ms_pck.Write(data_buff, 0, data_buff.Length);
            }

            byte[] info_byte = GetPaddedBuffer(ms_info.ToArray());
            byte[] pck_byte = ms_pck.ToArray();

            blowfish.Encrypt_LE(info_byte);

            BinaryWriter pck = new BinaryWriter(File.Create(out_file));
            pck.Write(Encoding.ASCII.GetBytes(MAGIC));
            pck.Write(data_list.Length);
            pck.Write(info_byte.Length);
            pck.Write(info_byte);
            pck.Write(pck_byte);
            pck.Close();
        }

        static string GetInfoFileString(BinaryReader info)
        {
            byte[] buff = new byte[255];
            for (int i = 0; i < buff.Length; i++)
            {
                buff[i] = info.ReadByte();
                if (buff[i] == 0x0)
                    return Encoding.ASCII.GetString(buff, 0, i);
            }
            return null;
        }

        static uint GetPaddedLength(uint length)
        {
            if (length % 8 == 0) return 0;
            return 8 - (length % 8);
        }

        static byte[] GetPaddedBuffer(byte[] buff)
        {
            int length = buff.Length;
            if (length % 8 == 0) return buff;
            int padded_length = 8 - (length % 8);
            return ByteCombine(buff, new byte[padded_length]);
        }

        public static byte[] ByteCombine(byte[] first, byte[] second)
        {
            return first.Concat(second).ToArray();
        }
    }
}
