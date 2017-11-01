﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.VisualBasic.Devices;

using DreamCube.Foundation.Log;

namespace ChangeFileEncoding
{
    class FileOperator
    {
        public event EventHandler<ExceptionEventArgs> Exception;
        public event EventHandler<ConvertedEventArgs> Converted;

        /// <summary>
        /// 给定一个路径，获取路径的所有文件
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public virtual async Task<IEnumerable<String>> GetAllFilesAsync(String path, String fileType)
        {
            return await Task.Run<IEnumerable<String>>(() =>
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        List<String> fileList = new List<String>();
                        if (!String.IsNullOrEmpty(fileType))
                        {
                            String[] fileTypes = fileType.Split(',');
                            foreach (String item in fileTypes)
                            {
                                String[] files = Directory.GetFiles(path, item, SearchOption.AllDirectories);
                                fileList.AddRange(files);
                            }
                        }
                        else
                        {
                            String[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                            fileList.AddRange(files);
                        }
                        return fileList;
                    }
                    else
                    {
                        return new String[] { path };
                    }
                }
                catch (Exception ex)
                {
                    FireExceptionEvent(null, ex);
                }
                return null;
            });
        }

        /// <summary>
        /// 转换文件类型
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="targetEnconding"></param>
        public async Task ChangeEncoding(String filePath, Encoding targetEnconding)
        {
            await Task.Run(() =>
            {
                try
                {
                    Log.Root.LogDebugFormat("begin changeFile:{0}", filePath);
                    //filePath = filePath.Replace("/", "\\");
                    //String fileName = filePath.Substring(filePath.LastIndexOf("\\"));
                    String fileExtension = Path.GetExtension(filePath);
                    String fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                    String tempFileName = filePath + "_temp" + fileExtension;
                    using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
                    {
                        Encoding oldEncoding;
                        //获取文件的编码格式
                        GetFileEncoding(fs, out oldEncoding);
                        if (oldEncoding == targetEnconding) return;
                        //这里可以改用StreamWriter和StreamReader，简单很多
                        Log.Root.LogDebugFormat("create and write file:{0}", tempFileName);
                        using (FileStream newfs = File.Create(tempFileName))
                        {
                            Byte[] buffer = new Byte[1024 * 1024];
                            Int32 read = 0;
                            Int32 offset = 0;
                            while (fs.Length > offset && (read = fs.Read(buffer, offset, buffer.Length)) > 0)
                            {
                                //循环的第一次，一定要处理文件头
                                String oldTxt = String.Empty;
                                if (offset == 0)
                                {
                                    Int32 fileHeadByteLength = GetFileHeadByteLength(oldEncoding);
                                    oldTxt = oldEncoding.GetString(buffer.Take(read).ToArray(), fileHeadByteLength, read - fileHeadByteLength);
                                    WriteFileHeader(newfs, targetEnconding);
                                }
                                else
                                {
                                    oldTxt = oldEncoding.GetString(buffer.Take(read).ToArray());
                                }
                                byte[] newTxtBytes = targetEnconding.GetBytes(oldTxt);
                                newfs.Write(newTxtBytes, offset, newTxtBytes.Length);
                                offset += read;
                            }
                        }
                    }
                    //文件写完之后，要重命名回去
                    File.Delete(filePath);
                    FileOperator.FileRename(tempFileName, fileNameWithoutExtension + fileExtension);
                    Log.Root.LogDebugFormat("end write file:{0}", tempFileName);
                    FireConvertedEvent(filePath, true);
                }
                catch (Exception ex)
                {
                    Log.Root.LogDebug("ex:", ex);
                    FireExceptionEvent(String.Empty, ex);
                }
            });
            return;
        }

        public static void FileRename(String filePath, String newName)
        {
            Computer myComputer = new Computer();
            myComputer.FileSystem.RenameFile(filePath, newName);
        }

        /// <summary>  
        /// 取得文件编码方式
        /// </summary>  
        /// <param name="fs"></param>  
        /// <param name="fileEncoding"></param>  
        /// <returns></returns>  
        private Boolean GetFileEncoding(FileStream fs, out Encoding fileEncoding)
        {
            Boolean find = false;
            fileEncoding = Encoding.Default;
            var buffer = new Byte[4];
            var read = fs.Read(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                find = true;
                fileEncoding = Encoding.Default;
            }
            else if (buffer[0] < 239)
            //if (buffer.Length <= 0 || buffer[0] < 239)
            {
                find = true;
                fileEncoding = Encoding.Default;
            }
            else if (buffer[0] == 239 && buffer[1] == 187 && buffer[2] == 191)
            {
                find = true;
                fileEncoding = Encoding.UTF8;
            }
            else if (buffer[0] == 254 && buffer[1] == byte.MaxValue)
            {
                find = true;
                fileEncoding = Encoding.BigEndianUnicode;
            }
            else if (buffer[0] == byte.MaxValue && buffer[1] == 254)
            {
                find = true;
                fileEncoding = Encoding.Unicode;
            }
            fs.Seek(0, SeekOrigin.Begin);
            return find;
        }

        /// <summary>
        /// 写入文件头（文件的编码格式）
        /// 用FileStream写入数据时，当采用非default编码的时候，则需要先写入文件头，否则会出现乱码
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="encoding"></param>
        private void WriteFileHeader(FileStream fs, Encoding encoding)
        {
            if (Equals(encoding, Encoding.UTF8))
            {
                fs.WriteByte(239);
                fs.WriteByte(187);
                fs.WriteByte(191);
            }
            else if (Equals(encoding, Encoding.BigEndianUnicode))
            {
                fs.WriteByte(254);
                fs.WriteByte(byte.MaxValue);
            }
            else if (Equals(encoding, Encoding.Unicode))
            {
                fs.WriteByte(byte.MaxValue);
                fs.WriteByte(254);
            }
        }

        /// <summary>
        /// 触发异常的事件
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="ex"></param>
        protected virtual void FireExceptionEvent(String filePath, Exception ex)
        {
            try
            {
                if (Exception != null)
                {
                    Exception.Invoke(null, new ExceptionEventArgs()
                    {
                        FilePath = filePath,
                        Ex = ex,
                        Msg = ex.Message
                    });
                }
            }
            catch (Exception)
            {
                //本demo忽略此异常
                //throw;
            }
        }

        /// <summary>
        /// 触发转换结果对象
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="result"></param>
        protected virtual void FireConvertedEvent(String filePath, Boolean result)
        {
            try
            {
                if (Converted != null)
                {
                    Converted.Invoke(null, new ConvertedEventArgs()
                    {
                        FilePath = filePath,
                        Result = result
                    });
                }
            }
            catch (Exception ex)
            {
                FireExceptionEvent(filePath, ex);
            }
        }

        /// <summary>
        /// 根据编码返回文件头的长度
        /// </summary>
        /// <param name="encoding"></param>
        /// <returns></returns>
        static Int32 GetFileHeadByteLength(Encoding encoding)
        {
            if (encoding == Encoding.UTF8) return 3;
            if (encoding == Encoding.Unicode || encoding == Encoding.BigEndianUnicode) return 2;
            return 0;
        }
    }
}
