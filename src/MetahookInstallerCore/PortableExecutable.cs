using System.Collections.Generic;
using System.IO;

namespace MetahookInstaller;

public static class PortableExecutable
{
    public static bool IsLegitimatePE(string dllPath)
    {
        if (!File.Exists(dllPath))
            return false;
        try
        {
            using var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var br = new BinaryReader(stream);
            if (stream.Length < 2 || stream.Length < 0x3C + 4)
                return false;
            if (br.ReadUInt16() != 0x5A4D)
                return false;
            stream.Position = 0x3C;
            int lfanew = br.ReadInt32();
            if (lfanew < 0 || lfanew + 4 > stream.Length)
                return false;
            stream.Position = lfanew;
            if (br.ReadUInt32() != 0x00004550)
                return false;
            return true;
        }
        catch
        {
            return false;
        }
    }
    private static string ReadNullTerminatedAscii(BinaryReader reader)
    {
        var bytes = new List<byte>();
        byte b;
        while ((b = reader.ReadByte()) != 0)
        {
            bytes.Add(b);
        }
        return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
    }
    private static long RvaToFileOffset(BinaryReader stream, int lfanew, uint rva)
    {
        try
        {
            stream.BaseStream.Position = lfanew + 4 + 20;
            ushort sizeOfOptionalHeader = stream.ReadUInt16();
            long sectionTableStart = lfanew + 4 + 20 + 2 + sizeOfOptionalHeader;
            stream.BaseStream.Position = lfanew + 4 + 2;
            ushort numberOfSections = stream.ReadUInt16();
            for (int i = 0; i < numberOfSections; i++)
            {
                long sectionPosition = sectionTableStart + i * 40;
                if (sectionPosition + 40 > stream.BaseStream.Length)
                    break;
                stream.BaseStream.Position = sectionPosition + 12;
                uint virtualAddress = stream.ReadUInt32();
                stream.BaseStream.Position = sectionPosition + 16;
                uint sizeOfRawData = stream.ReadUInt32();
                stream.BaseStream.Position = sectionPosition + 20;
                uint pointerToRawData = stream.ReadUInt32();
                if (rva >= virtualAddress && rva < virtualAddress + sizeOfRawData)
                {
                    return rva - virtualAddress + pointerToRawData;
                }
            }
            return -1;
        }
        catch
        {
            return -1;
        }
    }
    public static bool HasImportedModule(string dllPath, string targetModule)
    {
        if (!File.Exists(dllPath))
            return false;

        targetModule = targetModule.ToLowerInvariant();
        try
        {
            using var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(stream);
            if (stream.Length < 0x3C + 4)
                return false;
            stream.Position = 0x3C;
            int lfanew = reader.ReadInt32();
            if (lfanew + 4 > stream.Length)
                return false;
            stream.Position = lfanew;
            if (reader.ReadUInt32() != 0x00004550)
                return false;
            stream.Position = lfanew + 4; // 跳过 PE 签名
            ushort machine = reader.ReadUInt16();
            reader.ReadUInt16(); // 跳过 NumberOfSections
            reader.ReadUInt32(); // 跳过 TimeDateStamp
            reader.ReadUInt32(); // 跳过 PointerToSymbolTable
            reader.ReadUInt32(); // 跳过 NumberOfSymbols
            ushort sizeOfOptionalHeader = reader.ReadUInt16();
            reader.ReadUInt16(); // 跳过 Characteristics
            long optionalHeaderStart = stream.Position;
            bool is64Bit = machine == 0x8664; // 0x8664 表示 x64
            int dataDirectoryOffset = is64Bit ? 224 : 208;
            if (optionalHeaderStart + dataDirectoryOffset + 8 > stream.Length)
                return false; // 数据目录表位置无效

            // 5. 读取导入表在数据目录中的地址（相对虚拟地址 RVA）和大小
            stream.Position = optionalHeaderStart + dataDirectoryOffset;
            uint importTableRva = reader.ReadUInt32(); // 导入表的 RVA
            uint importTableSize = reader.ReadUInt32(); // 导入表大小（若为0则无导入表）
            if (importTableSize == 0)
                return false;

            // 6. 将 RVA 转换为文件偏移量（需要通过节表计算）
            long importTableFileOffset = RvaToFileOffset(reader, lfanew, importTableRva);
            if (importTableFileOffset == -1)
                return false;

            // 7. 遍历导入表中的每个 IMAGE_IMPORT_DESCRIPTOR
            stream.Position = importTableFileOffset;
            while (true)
            {
                // 读取一个导入描述符（简化版，仅关注名称 RVA）
                uint originalFirstThunk = reader.ReadUInt32();
                reader.ReadUInt32(); // 跳过 TimeDateStamp
                reader.ReadUInt32(); // 跳过 ForwarderChain
                uint nameRva = reader.ReadUInt32(); // 模块名称的 RVA
                reader.ReadUInt32(); // 跳过 FirstThunk

                // 若所有字段为0，则表示导入表结束
                if (originalFirstThunk == 0 && nameRva == 0)
                    break;

                // 8. 解析模块名称
                if (nameRva == 0)
                    continue;
                long nameFileOffset = RvaToFileOffset(reader, lfanew, nameRva);
                if (nameFileOffset == -1)
                    continue;
                stream.Position = nameFileOffset;
                string moduleName = ReadNullTerminatedAscii(reader).ToLowerInvariant();
                if (moduleName == targetModule)
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
