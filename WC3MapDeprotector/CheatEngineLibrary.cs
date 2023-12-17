using System.Runtime.InteropServices;

namespace WC3MapDeprotector
{
    public enum TScanOption { soUnknownValue = 0, soExactValue = 1, soValueBetween = 2, soBiggerThan = 3, soSmallerThan = 4, soIncreasedValue = 5, soIncreasedValueBy = 6, soDecreasedValue = 7, soDecreasedValueBy = 8, soChanged = 9, soUnchanged = 10, soCustom };
    public enum TScanType { stNewScan, stFirstScan, stNextScan };
    public enum TRoundingType { rtRounded = 0, rtExtremerounded = 1, rtTruncated = 2 };
    public enum TVariableType { vtByte = 0, vtWord = 1, vtDword = 2, vtQword = 3, vtSingle = 4, vtDouble = 5, vtString = 6, vtUnicodeString = 7, vtByteArray = 8, vtBinary = 9, vtAll = 10, vtAutoAssembler = 11, vtPointer = 12, vtCustom = 13, vtGrouped = 14, vtByteArrays = 15 }; //all ,grouped and MultiByteArray are special types
    public enum TCustomScanType { cstNone, cstAutoAssembler, cstCPP, cstDLLFunction };
    public enum TFastScanMethod { fsmNotAligned = 0, fsmAligned = 1, fsmLastDigits = 2 };
    public enum Tscanregionpreference { scanDontCare, scanExclude, scanInclude };

    public class CheatEngineLibrary : IDisposable
    {
        [DllImport("kernel32.dll", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
        extern static IntPtr LoadLibraryW(String strLib);

        [DllImport("kernel32.dll")]
        extern static IntPtr LoadLibrary(String strLib);

        [DllImport("kernel32.dll")]
        extern static int FreeLibrary(IntPtr iModule);

        [DllImport("kernel32.dll", EntryPoint = "GetProcAddress", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Winapi)]
        extern static IntPtr GetProcAddress(IntPtr iModule, String strProcName);

        public delegate void IGetProcessList([MarshalAs(UnmanagedType.BStr)] out string processes);
        public delegate void IOpenProcess([MarshalAs(UnmanagedType.BStr)] string pid);

        public delegate void IResetTable();
        public delegate void IAddScript([MarshalAs(UnmanagedType.BStr)] string name, [MarshalAs(UnmanagedType.BStr)] string script);
        public delegate void IActivateRecord(int id, bool activate);
        public delegate void IRemoveRecord(int id);
        public delegate void IApplyFreeze();

        public delegate void IAddAddressManually([MarshalAs(UnmanagedType.BStr)] string initialaddress, TVariableType vartype);
        public delegate void IGetValue(int id, [MarshalAs(UnmanagedType.BStr)] out string value);
        public delegate void ISetValue(int id, [MarshalAs(UnmanagedType.BStr)] string value, bool freezer);
        public delegate void IProcessAddress([MarshalAs(UnmanagedType.BStr)] string address, TVariableType vartype, bool showashexadecimal, bool showAsSigned, int bytesize, [MarshalAs(UnmanagedType.BStr)] out string value);

        public delegate void IInitMemoryScanner(int handle);
        public delegate void INewScan();
        public delegate void IConfigScanner(Tscanregionpreference scanWritable, Tscanregionpreference scanExecutable, Tscanregionpreference scanCopyOnWrite);

        public delegate void IFirstScan(TScanOption scanOption, TVariableType variableType, TRoundingType roundingtype, [MarshalAs(UnmanagedType.BStr)] string scanvalue1, [MarshalAs(UnmanagedType.BStr)] string scanvalue2, [MarshalAs(UnmanagedType.BStr)] string startaddress, [MarshalAs(UnmanagedType.BStr)] string stopaddress, bool hexadecimal, bool binaryStringAsDecimal, bool unicode, bool casesensitive, TFastScanMethod fastscanmethod, [MarshalAs(UnmanagedType.BStr)] string fastscanparameter);

        public delegate void INextScan(TScanOption scanOption, TRoundingType roundingtype, [MarshalAs(UnmanagedType.BStr)] string scanvalue1, [MarshalAs(UnmanagedType.BStr)] string scanvalue2, bool hexadecimal, bool binaryStringAsDecimal, bool unicode, bool casesensitive, bool percentage, bool compareToSavedScan, [MarshalAs(UnmanagedType.BStr)] string savedscanname);

        public delegate Int64 ICountAddressesFound();
        public delegate void IGetAddress(Int64 index, [MarshalAs(UnmanagedType.BStr)] out string address, [MarshalAs(UnmanagedType.BStr)] out string value);
        public delegate void IInitFoundList(TVariableType vartype, int varlength, bool hexadecimal, bool signed, bool binaryasdecimal, bool unicode);
        public delegate void IResetValues();
        public delegate int IGetBinarySize();

        public IGetProcessList iGetProcessList;
        public IOpenProcess iOpenProcess;

        public IResetTable iResetTable;
        public IAddScript iAddScript;
        public IRemoveRecord iRemoveRecord;
        public IActivateRecord iActivateRecord;
        public IApplyFreeze iApplyFreeze;

        public IAddAddressManually iAddAddressManually;
        public IGetValue iGetValue;
        public ISetValue iSetValue;
        public IProcessAddress iProcessAddress;


        public IInitMemoryScanner iInitMemoryScanner;
        public INewScan iNewScan;
        public IConfigScanner iConfigScanner;
        public IFirstScan iFirstScan;
        public INextScan iNextScan;
        public ICountAddressesFound iCountAddressesFound;
        public IGetAddress iGetAddress;
        public IInitFoundList iInitFoundList;
        public IResetValues iResetValues;
        public IGetBinarySize iGetBinarySize;

        public void Dispose()
        {
            FreeLibrary(library);
        }

        protected IntPtr library;
        public CheatEngineLibrary()
        {
            library = IntPtr.Size == 4 ? LoadLibrary("ce-lib32.dll") : LoadLibraryW("ce-lib64.dll");
            IntPtr pGetProcessList = GetProcAddress(library, "IGetProcessList");
            IntPtr pOpenProcess = GetProcAddress(library, "IOpenProcess");

            IntPtr pResetTable = GetProcAddress(library, "IResetTable");
            IntPtr pAddScript = GetProcAddress(library, "IAddScript");
            IntPtr pRemoveRecord = GetProcAddress(library, "IRemoveRecord");
            IntPtr pActivateRecord = GetProcAddress(library, "IActivateRecord");
            IntPtr pApplyFreeze = GetProcAddress(library, "IApplyFreeze");

            IntPtr pAddAddressManually = GetProcAddress(library, "IAddAddressManually");
            IntPtr pGetValue = GetProcAddress(library, "IGetValue");
            IntPtr pSetValue = GetProcAddress(library, "ISetValue");
            IntPtr pProcessAddress = GetProcAddress(library, "IProcessAddress");

            IntPtr pInitMemoryScanner = GetProcAddress(library, "IInitMemoryScanner");
            IntPtr pNewScan = GetProcAddress(library, "INewScan");
            IntPtr pConfigScanner = GetProcAddress(library, "IConfigScanner");
            IntPtr pFirstScan = GetProcAddress(library, "IFirstScan");
            IntPtr pNextScan = GetProcAddress(library, "INextScan");
            IntPtr pCountAddressesFound = GetProcAddress(library, "ICountAddressesFound");
            IntPtr pGetAddress = GetProcAddress(library, "IGetAddress");
            IntPtr pInitFoundList = GetProcAddress(library, "IInitFoundList");
            IntPtr pResetValues = GetProcAddress(library, "IResetValues");
            IntPtr pGetBinarySize = GetProcAddress(library, "IGetBinarySize");

            iGetProcessList = (IGetProcessList)Marshal.GetDelegateForFunctionPointer(pGetProcessList, typeof(IGetProcessList));
            iOpenProcess = (IOpenProcess)Marshal.GetDelegateForFunctionPointer(pOpenProcess, typeof(IOpenProcess));

            iResetTable = (IResetTable)Marshal.GetDelegateForFunctionPointer(pResetTable, typeof(IResetTable));
            iAddScript = (IAddScript)Marshal.GetDelegateForFunctionPointer(pAddScript, typeof(IAddScript));
            iRemoveRecord = (IRemoveRecord)Marshal.GetDelegateForFunctionPointer(pRemoveRecord, typeof(IRemoveRecord));
            iActivateRecord = (IActivateRecord)Marshal.GetDelegateForFunctionPointer(pActivateRecord, typeof(IActivateRecord));
            iApplyFreeze = (IApplyFreeze)Marshal.GetDelegateForFunctionPointer(pApplyFreeze, typeof(IApplyFreeze));

            iAddAddressManually = (IAddAddressManually)Marshal.GetDelegateForFunctionPointer(pAddAddressManually, typeof(IAddAddressManually));
            iGetValue = (IGetValue)Marshal.GetDelegateForFunctionPointer(pGetValue, typeof(IGetValue));
            iSetValue = (ISetValue)Marshal.GetDelegateForFunctionPointer(pSetValue, typeof(ISetValue));
            iProcessAddress = (IProcessAddress)Marshal.GetDelegateForFunctionPointer(pProcessAddress, typeof(IProcessAddress));

            iInitMemoryScanner = (IInitMemoryScanner)Marshal.GetDelegateForFunctionPointer(pInitMemoryScanner, typeof(IInitMemoryScanner));
            iNewScan = (INewScan)Marshal.GetDelegateForFunctionPointer(pNewScan, typeof(INewScan));
            iConfigScanner = (IConfigScanner)Marshal.GetDelegateForFunctionPointer(pConfigScanner, typeof(IConfigScanner));
            iFirstScan = (IFirstScan)Marshal.GetDelegateForFunctionPointer(pFirstScan, typeof(IFirstScan));
            iNextScan = (INextScan)Marshal.GetDelegateForFunctionPointer(pNextScan, typeof(INextScan));

            iCountAddressesFound = (ICountAddressesFound)Marshal.GetDelegateForFunctionPointer(pCountAddressesFound, typeof(ICountAddressesFound));
            iGetAddress = (IGetAddress)Marshal.GetDelegateForFunctionPointer(pGetAddress, typeof(IGetAddress));
            iInitFoundList = (IInitFoundList)Marshal.GetDelegateForFunctionPointer(pInitFoundList, typeof(IInitFoundList));
            iResetValues = (IResetValues)Marshal.GetDelegateForFunctionPointer(pResetValues, typeof(IResetValues));
            iGetBinarySize = (IGetBinarySize)Marshal.GetDelegateForFunctionPointer(pGetBinarySize, typeof(IGetBinarySize));
        }
    }
}