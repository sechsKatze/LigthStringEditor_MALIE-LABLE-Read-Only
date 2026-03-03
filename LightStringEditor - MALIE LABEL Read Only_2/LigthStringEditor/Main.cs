using AdvancedBinary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LigthStringEditor
{
    /// <summary>
    /// LightStringEditor v2.0 - MALIE_LABEL 읽기 전용화
    /// 
    /// 변경사항:
    /// - MALIE_LABEL: 읽기 전용 (Import만 가능, Export시 원본 그대로)
    /// - StringTable: 편집 가능 (기존 기능 유지)
    /// - 디버그 로그 제거
    /// </summary>
    public class LightDat {
        
        private bool LastLengthCheck = false;
        private StructReader Script;
        public LightDat(byte[] Script) {
            this.Script = new StructReader(new MemoryStream(Script), false, Encoding.Unicode);
        }

        Encoding Encoding = Encoding.Unicode;
        long StringTablePos = 0;
        long OffsetTablePos = 0;
        long MalieLabelStart = 0;
        long MalieLabelEnd = 0;
        private int malieLabelCount = 0;
        
        private Dictionary<int, byte[]> originalMalieLabelBytes = new Dictionary<int, byte[]>();
        private Dictionary<int, byte[]> originalStringBytes = new Dictionary<int, byte[]>();
        private List<string> originalMalieLabelStrings = new List<string>();
        private List<string> originalTableStrings = new List<string>();
        private List<bool> malieLabelPadding = new List<bool>();
        
        public int MalieLabelCount => malieLabelCount;
        
        public string[] Import() {
            try {
                Script.Seek(0, SeekOrigin.Begin);
                
                if (StringTablePos == 0)
                    StringTablePos = FindStringTablePos();
                
                if (OffsetTablePos == 0)
                    OffsetTablePos = FindOffsetTable();

                if (MalieLabelStart == 0)
                    FindMalieLabelRegion();

                List<string> allStrings = new List<string>();
                malieLabelPadding.Clear();
                originalMalieLabelBytes.Clear();
                originalMalieLabelStrings.Clear();
                originalStringBytes.Clear();
                originalTableStrings.Clear();
                
                // MALIE_LABEL 읽기 (읽기 전용)
                Script.Seek(MalieLabelStart, SeekOrigin.Begin);
                malieLabelCount = 0;
                
                while (Script.BaseStream.Position < MalieLabelEnd) {
                    List<byte> buffer = new List<byte>();
                    long strStart = Script.BaseStream.Position;
                    
                    while (Script.BaseStream.Position < MalieLabelEnd) {
                        byte b1 = Script.ReadByte();
                        byte b2 = Script.ReadByte();
                        
                        if (b1 == 0 && b2 == 0) {
                            break;
                        }
                        
                        buffer.Add(b1);
                        buffer.Add(b2);
                    }
                    
                    if (buffer.Count > 0) {
                        int bytecodeScore = 0;
                        for (int i = 0; i < Math.Min(20, buffer.Count); i += 2) {
                            byte b = buffer[i];
                            if (b == 0x0E || b == 0x06 || b == 0x07 || b == 0x02 || b == 0x04 || b == 0x11 || b == 0x0D) {
                                bytecodeScore++;
                            }
                        }
                        
                        if (bytecodeScore >= 5) {
                            break;
                        }
                        
                        originalMalieLabelBytes[malieLabelCount] = buffer.ToArray();
                        
                        string text = Encoding.GetString(buffer.ToArray());
                        originalMalieLabelStrings.Add(text);
                        allStrings.Add(text);
                        malieLabelCount++;
                        
                        bool hasExtraPadding = false;
                        if (Script.BaseStream.Position < MalieLabelEnd - 2) {
                            long currentPos = Script.BaseStream.Position;
                            byte p1 = Script.ReadByte();
                            byte p2 = Script.ReadByte();
                            
                            if (p1 == 0 && p2 == 0) {
                                hasExtraPadding = true;
                            } else {
                                Script.Seek(currentPos, SeekOrigin.Begin);
                            }
                        }
                        
                        malieLabelPadding.Add(hasExtraPadding);
                    }
                }

                // StringTable 읽기
                Script.Seek(OffsetTablePos, SeekOrigin.Begin);
                uint Count = Script.ReadUInt32();
                StrEntry[] Entries = new StrEntry[Count];
                
                for (uint i = 0; i < Entries.LongLength; i++) {
                    Script.ReadStruct(ref Entries[i]);
                }

                for (uint i = 0; i < Entries.LongLength; i++) {
                    Script.Seek(Entries[i].Offset + StringTablePos + 4, SeekOrigin.Begin);
                    List<byte> Buffer = new List<byte>();
                    uint length = Entries[i].Length;
                    while (length-- > 0)
                        Buffer.Add(Script.ReadByte());
                    
                    originalStringBytes[(int)i] = Buffer.ToArray();
                    
                    string text = Encoding.GetString(Buffer.ToArray());
                    originalTableStrings.Add(text);
                    allStrings.Add(text);
                }

                return allStrings.ToArray();
            }
            catch (Exception ex) {
                if (LastLengthCheck)
                    throw ex;
                LastLengthCheck = true;
                OffsetTablePos = 0;
                StringTablePos = 0;
                MalieLabelStart = 0;
                MalieLabelEnd = 0;
                malieLabelPadding.Clear();
                originalMalieLabelBytes.Clear();
                originalMalieLabelStrings.Clear();
                originalStringBytes.Clear();
                originalTableStrings.Clear();
                return Import();
            }
        }


        public byte[] Export(string[] Strings) {
            string[] labelStrings = Strings.Take(malieLabelCount).ToArray();
            string[] tableStrings = Strings.Skip(malieLabelCount).ToArray();
            
            MemoryStream Output = new MemoryStream();
            Script.Seek(0, SeekOrigin.Begin);
            
            // 1. Header 복사
            CopyStream(Script.BaseStream, Output, MalieLabelStart);

            // 2. MALIE_LABEL - 원본 그대로 출력 (편집 사항 무시)
            Script.Seek(MalieLabelStart, SeekOrigin.Begin);
            long malieLabelSize = MalieLabelEnd - MalieLabelStart;
            byte[] malieLabelData = new byte[malieLabelSize];
            Script.BaseStream.Read(malieLabelData, 0, (int)malieLabelSize);
            Output.Write(malieLabelData, 0, malieLabelData.Length);

            // 3. 바이트코드 영역 복사 (수정 없음)
            Script.Seek(MalieLabelEnd, SeekOrigin.Begin);
            long bytecodeSize = OffsetTablePos - MalieLabelEnd;
            byte[] bytecode = new byte[bytecodeSize];
            Script.BaseStream.Read(bytecode, 0, (int)bytecodeSize);
            Output.Write(bytecode, 0, bytecode.Length);

            // 4. StringTable 출력
            MemoryStream StrBuffer = new MemoryStream();
            StructWriter StrWorker = new StructWriter(StrBuffer, false, Encoding.Unicode);

            MemoryStream OffBuffer = new MemoryStream();
            StructWriter OffWorker = new StructWriter(OffBuffer, false, Encoding.Unicode);

            OffWorker.Write((uint)tableStrings.LongLength);

            for (int i = 0; i < tableStrings.Length; i++) {
                StrEntry Entry = new StrEntry() {
                    Offset = (uint)StrBuffer.Length,
                    Length = (uint)(tableStrings[i].Length * 2)
                };
                OffWorker.WriteStruct(ref Entry);
                
                if (i < originalTableStrings.Count && tableStrings[i] == originalTableStrings[i]) {
                    StrBuffer.Write(originalStringBytes[i], 0, originalStringBytes[i].Length);
                    StrBuffer.WriteByte(0);
                    StrBuffer.WriteByte(0);
                } else {
                    StrWorker.Write(tableStrings[i], StringStyle.UCString);
                }
            }

            OffWorker.Write((uint)StrBuffer.Length);

            StrBuffer.Position = 0;
            OffBuffer.Position = 0;

            CopyStream(OffBuffer, Output, OffBuffer.Length);
            CopyStream(StrBuffer, Output, StrBuffer.Length);

            StrWorker.Close();
            OffWorker.Close();
            
            return Output.ToArray();
        }
        
        private void CopyStream(Stream Input, Stream Output, long Len) {
            long Readed = 0;
            while (Readed < Len) {
                byte[] Buffer = new byte[Readed + 1024 > Len ? Len - Readed : 1024];
                int r = Input.Read(Buffer, 0, Buffer.Length);
                Output.Write(Buffer, 0, r);
                Readed += r;
                if (r == 0)
                    throw new Exception("Failed to Read the Stream");
            }
        }

        private struct StrEntry {
            internal uint Offset;
            internal uint Length;            
        }

        private void FindMalieLabelRegion()
        {
            byte[] signature = Encoding.GetBytes("MALIE_LABEL");
            byte[] buffer = new byte[signature.Length];
            
            Script.Seek(0, SeekOrigin.Begin);
            
            while (Script.BaseStream.Position < Script.BaseStream.Length - signature.Length)
            {
                long currentPos = Script.BaseStream.Position;
                Script.BaseStream.Read(buffer, 0, signature.Length);
                
                bool match = true;
                for (int i = 0; i < signature.Length; i++)
                {
                    if (buffer[i] != signature[i])
                    {
                        match = false;
                        break;
                    }
                }
                
                if (match)
                {
                    long sigPos = currentPos;
                    
                    for (long i = sigPos - 2; i >= Math.Max(0, sigPos - 200); i -= 2)
                    {
                        Script.Seek(i, SeekOrigin.Begin);
                        byte b1 = Script.ReadByte();
                        byte b2 = Script.ReadByte();
                        
                        if (b1 == 0 && b2 == 0)
                        {
                            MalieLabelStart = i + 2;
                            break;
                        }
                    }
                    
                    if (MalieLabelStart == 0)
                        MalieLabelStart = sigPos;
                    
                    Script.Seek(MalieLabelStart, SeekOrigin.Begin);
                    
                    long pos = MalieLabelStart;
                    int consecutiveNulls = 0;
                    int stringCount = 0;
                    
                    while (pos < OffsetTablePos - 100 && stringCount < 100000)
                    {
                        long strStart = pos;
                        List<byte> strBytes = new List<byte>();
                        
                        while (pos < OffsetTablePos - 100)
                        {
                            Script.Seek(pos, SeekOrigin.Begin);
                            byte b1 = Script.ReadByte();
                            byte b2 = Script.ReadByte();
                            pos += 2;
                            
                            if (b1 == 0 && b2 == 0)
                            {
                                break;
                            }
                            
                            strBytes.Add(b1);
                            strBytes.Add(b2);
                            
                            if (strBytes.Count > 500)
                            {
                                break;
                            }
                        }
                        
                        if (strBytes.Count == 0)
                        {
                            consecutiveNulls++;
                            if (consecutiveNulls >= 3)
                            {
                                MalieLabelEnd = pos - consecutiveNulls * 2;
                                return;
                            }
                            continue;
                        }
                        else
                        {
                            consecutiveNulls = 0;
                        }
                        
                        int bytecodeScore = 0;
                        for (int i = 0; i < Math.Min(20, strBytes.Count); i += 2)
                        {
                            byte b = strBytes[i];
                            if (b == 0x0E || b == 0x06 || b == 0x07 || b == 0x02 || b == 0x04 || b == 0x11 || b == 0x0D)
                            {
                                bytecodeScore++;
                            }
                        }
                        
                        if (bytecodeScore >= 5)
                        {
                            MalieLabelEnd = strStart;
                            return;
                        }
                        
                        stringCount++;
                    }
                    
                    MalieLabelEnd = pos;
                    return;
                }
                
                Script.Seek(currentPos + 2, SeekOrigin.Begin);
            }
            
            throw new Exception("Failed to find MALIE_LABEL signature");
        }

        private long FindOffsetTable() {
            while (Script.PeekInt32() != 0)
                Script.Seek(-8, SeekOrigin.Current);
            Script.Seek(-4, SeekOrigin.Current);
            return Script.BaseStream.Position;
        }
        
        uint LastStringLen = 0;
        private long FindStringTablePos() {
            if (LastLengthCheck) {
                LastStringLen = 2;
                do {
                    LastStringLen += 2;
                    Script.Seek(LastStringLen * -1, SeekOrigin.End);
                } while (Script.PeekInt16() != 0);
                LastStringLen -= 4;

                do {
                    Script.Seek(-5, SeekOrigin.Current);
                } while (Script.ReadInt32() != LastStringLen || Script.PeekInt32() != StrTblLen);

            } else {
                Script.Seek(-4, SeekOrigin.End);
                while (Script.PeekInt32() != StrTblLen) {
                    Script.Seek(-2, SeekOrigin.Current);
                }
            }
            return Script.BaseStream.Position;
        }

        private long StrTblLen { get { return Script.BaseStream.Length - Script.BaseStream.Position - 4; } }
    }
}