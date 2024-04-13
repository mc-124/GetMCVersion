using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace McVersion
{
    public class McVersion
    {
        static bool BytesArrayIn(byte[] mainBytes, byte[] array)
        {
            for (int i = 0; i <= mainBytes.Length - array.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < array.Length; j++)
                {
                    if (mainBytes[i + j] != array[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return true;
            }
            return false;
        }
        static int FindIndex(byte[] mainBytes, byte[] array)
        {
            for (int i = 0; i <= mainBytes.Length - array.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < array.Length; j++)
                {
                    if (mainBytes[i + j] != array[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }
            return -1;
        }
        bool ByteArrayIn(byte[] mainBytes,int Byte)
        {
            foreach (byte b in mainBytes)
            {
                if (b == Byte) return true;
            }
            return false;
        }

        /// <summary>
        /// 获取MC版本号 注：仅支持原版端
        /// </summary>
        /// <param name="clientJar">MC的client.jar的路径</param>
        /// <returns>获取到的版本号(string)</returns>
        public string Get(string clientJar)
        {
            object versionJson = null;
            object minecraftClass = null;
            object mcServerClass = null;

            using (ZipArchive z = ZipFile.OpenRead(clientJar))
            {
                foreach (ZipArchiveEntry entry in z.Entries)
                {
                    if (entry.FullName == "version.json")
                    {
                        versionJson = entry;
                    } else if (entry.FullName == "net/minecraft/client/Minecraft.class")
                    {
                        minecraftClass = entry;
                    } else if (entry.FullName == "net/minecraft/server/MinecraftServer.class")
                    {
                        mcServerClass = entry;
                    };
                };
                if (versionJson == null&&minecraftClass == null&&mcServerClass == null)
                {
                    throw new FileNotFoundException("找不到version.json,Minecraft.class,MinecraftServer.class中的任何一项");
                };
                if (versionJson is ZipArchiveEntry)
                {
                    //Console.WriteLine("mode: json");
                    ZipArchiveEntry entry = versionJson as ZipArchiveEntry;
                    using (Stream entryStream = entry.Open())
                    {
                        using (StreamReader sr = new StreamReader(entryStream))
                        {
                            string jsonString = sr.ReadToEnd();
                            //dynamic jsonData = JsonConvert.DeserializeObject(jsonString);
                            JObject json = JObject.Parse(jsonString);
                            return json["name"].ToString();
                        };
                    };
                } else if (minecraftClass is ZipArchiveEntry)
                {
                    //Console.WriteLine("mode: minecraft.class");
                    ZipArchiveEntry entry = minecraftClass as ZipArchiveEntry;
                    using (Stream entryStream = entry.Open())
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            entryStream.CopyTo(ms);
                            byte[] bytes = ms.ToArray();
                            byte[] splitBytes = {77, 105, 110, 101, 99, 114, 97, 102, 116, 32, 77, 105, 110, 101, 99, 114, 97, 102, 116, 32};
                            List<byte> version = new List<byte>();
                            int index = FindIndex(bytes, splitBytes);
                            if (index == -1)
                            {
                                throw new Exception("client.jar可能已损坏");
                            }
                            index += splitBytes.Length;
                            foreach (byte b in bytes.ToList().Skip(index))
                            {
                                if (b == 1)
                                {
                                    break;
                                }
                                version.Add(b);
                            }
                            return Encoding.UTF8.GetString(version.ToArray());
                        };
                    };
                } else // mcServer is ZipArchiveEntry
                {
                    //Console.WriteLine("mode: minecraftServer.class");
                    ZipArchiveEntry entry = mcServerClass as ZipArchiveEntry;
                    using (Stream entryStream = entry.Open())
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            entryStream.CopyTo(ms);
                            byte[] bytes = ms.ToArray();

                            // 判断用字节串
                            byte[] bs = {1, 0, 1, 46, 1, 0, 1, 47, 1, 0 };

                            bool bytesIn = BytesArrayIn(bytes, bs);
                            
                            // 04131234-BUG: C#获取到乱码，python正常获取到版本号
                            if (bytesIn) // version < 1.8
                            {
                                //Console.WriteLine("version < 1.8");
                                List<byte> bytesl = bytes.ToList();
                                List<byte> versionBytes = new List<byte>(); // 版本号字节串

                                int index = FindIndex(bytes, bs);
                                if (index == -1)
                                {
                                    throw new Exception("client.jar可能已损坏");
                                };
                                index += (bs.Length + 1);
                                foreach (byte b in bytesl.Skip(index))
                                {
                                    if (b == 1)
                                    {
                                        break;
                                    } else
                                    {
                                        versionBytes.Add(b);
                                    }
                                }
                                string Return = Encoding.UTF8.GetString(versionBytes.ToArray());
                                switch (Return)
                                {
                                    case "1.0.0":
                                        Return = "1.0";
                                        break;
                                    case "RC2":
                                        Return = "1.0.0-rc2-2";
                                        break;
                                };
                                return Return;
                            }
                            else // version >= 1.8
                            {
                                //Console.WriteLine("version >= 1.8");
                                int len = bs.Length;
                                List<List<byte>> splitResults = new List<List<byte>>
                                {
                                    new List<byte>()
                                };
                                foreach (byte b in bytes) // split
                                {
                                    if (b == 10) // "\n" = 10
                                    {
                                        splitResults.Add(new List<byte>());
                                    }
                                    else
                                    {
                                        splitResults[splitResults.Count - 1].Add(b);
                                    }
                                };
                                List<List<byte>> results = new List<List<byte>>();
                                byte[] bytesPre = new byte[] { 112, 114, 101 };
                                byte[] bytesPRE = new byte[] { 80, 114, 101 };
                                byte[] bytes2 = new byte[] { 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 113, 119, 101, 114, 116, 121, 117, 105, 111, 112, 97, 115, 100, 102, 103, 104, 106, 107, 108, 122, 120, 99, 118, 98, 110, 109, 46, 45, 95, 32, 126, 81, 87, 69, 82, 84, 89, 85, 73, 79, 80, 65, 83, 68, 70, 71, 72, 74, 75, 76, 90, 88, 67, 86, 66, 78, 77 };
                                foreach (List<byte> splitBytes in splitResults)
                                {
                                    byte[] Bytes = splitBytes.ToArray();
                                    if (splitBytes.Count < 50 && 
                                        (ByteArrayIn(Bytes,46) || ByteArrayIn(Bytes,119) || BytesArrayIn(Bytes,bytesPre) || BytesArrayIn(Bytes,bytesPRE)) &&
                                        (ByteArrayIn(Bytes,48) || ByteArrayIn(Bytes,49) || ByteArrayIn(Bytes,50) || ByteArrayIn(Bytes,51) || ByteArrayIn(Bytes,52) || ByteArrayIn(Bytes,53) ||
                                         ByteArrayIn(Bytes,54) || ByteArrayIn(Bytes,55) || ByteArrayIn(Bytes,56) || ByteArrayIn(Bytes,57)))
                                    {
                                        List<List<byte>> c = new List<List<byte>>
                                        {
                                            new List<byte>()
                                        };
                                        foreach (byte r in splitBytes)
                                        {
                                            if (ByteArrayIn(bytes2, r))
                                            {
                                                c[c.Count - 1].Add(r);
                                            } else
                                            {
                                                if (c[c.Count - 1].Count != 0)
                                                {
                                                    c.Add(new List<byte>());
                                                }
                                            }
                                        };
                                        if (c.Count == 1)
                                        {
                                            if (c[0].Count != 0)
                                            {
                                                foreach (List<byte> _c in c)
                                                {
                                                    results.Add(_c);
                                                }
                                            }
                                        }else // > 1
                                        {
                                            foreach (List<byte> _c in c)
                                            {
                                                results.Add(_c);
                                            }
                                        };
                                    }
                                };
                                foreach (List<byte> resu in results)
                                {
                                    foreach (int i in Enumerable.Range(48,10))
                                    {
                                        byte[] rb = resu.ToArray();
                                        if (ByteArrayIn(rb, i) && 
                                            (BytesArrayIn(rb,bytesPre) || ByteArrayIn(rb,46) || ByteArrayIn(rb,119))&&
                                            resu[resu.Count - 1] != 46 && resu.Count > 2)
                                        {
                                            //Console.WriteLine("");
                                            return Encoding.UTF8.GetString(rb);
                                        }
                                    }
                                };
                                throw new Exception("无法确定Minecraft版本");
                            };
                        }
                    };
                }
                
            }
        }
    }
}