using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace PCBSJS;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private ConfigEntry<KeyboardShortcut> DumpUserDataKey { get; set; }
    private ConfigEntry<KeyboardShortcut> DumpDatabaseKey { get; set; }
    private ConfigEntry<KeyboardShortcut> DefaultSortKey { get; set; }
    private ConfigEntry<KeyboardShortcut> NewestSortKey { get; set; }
    private ConfigEntry<KeyboardShortcut> CostAscSortKey { get; set; }
    private ConfigEntry<KeyboardShortcut> CostDescSortKey { get; set; }
    private readonly Harmony harmony = new Harmony("PCBSJS");

    private static int inventorySort = 0;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        DumpUserDataKey = Config.Bind("General", "DumpUserDataKey", new KeyboardShortcut(KeyCode.F5), new ConfigDescription("Press this key to dump inventory, job, etc as JSON."));
        DumpDatabaseKey = Config.Bind("General", "DumpDatabaseKey", new KeyboardShortcut(KeyCode.F6), new ConfigDescription("Press this key to dump inventory, job, etc as JSON."));
        
        DefaultSortKey = Config.Bind("General", "DefaultSortKey", new KeyboardShortcut(KeyCode.F1), new ConfigDescription("Sort inventory by default."));
        NewestSortKey = Config.Bind("General", "NewestSortKey", new KeyboardShortcut(KeyCode.F2), new ConfigDescription("Sort inventory by newest first."));
        CostAscSortKey = Config.Bind("General", "CostAscSortKey", new KeyboardShortcut(KeyCode.F3), new ConfigDescription("Sort inventory by price asc."));
        CostDescSortKey = Config.Bind("General", "CostDescSortKey", new KeyboardShortcut(KeyCode.F4), new ConfigDescription("Sort inventory by price desc."));

        harmony.PatchAll();
    }

    private void Update()
    {
        if (DumpUserDataKey.Value.IsDown())
        {
            DumpUserData();
        }
        if (DumpDatabaseKey.Value.IsDown())
        {
            DumpDatabase();
        }
        if (DefaultSortKey.Value.IsDown())
        {
            inventorySort = 0;
        }
        if (NewestSortKey.Value.IsDown())
        {
            inventorySort = 1;
        }
        if (CostAscSortKey.Value.IsDown())
        {
            inventorySort = 2;
        }
        if (CostDescSortKey.Value.IsDown())
        {
            inventorySort = 3;
        }
    }

    private void OnDestroy()
    {
        harmony.UnpatchSelf();
    }

    static string ParseComputerSave(ComputerSave cs)
    {
        static string ParsePartsList(PartInstance[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return "[]";
            }
            return "[" + parts.Where(p => p != null).Join(part => ParsePartInstance(part), ",") + "]";
        }
        string computerSaveString = "{" +
            "\"case\":" + ParsePartInstance(cs.caseID) + "" +
            ",\"motherboard\":" + ParsePartInstance(cs.motherboardID) + "" +
            ",\"cpu\":" + ParsePartInstance(cs.cpuID) + "" +
            ",\"cooler\":" + ParsePartInstance(cs.cpuCoolerID) + "" +
            ",\"psu\":" + ParsePartInstance(cs.psuID) + "" +
            ",\"storage\":" + ParsePartsList(cs.storageSlots) + "" +
            ",\"caseFan\":" + ParsePartsList(cs.caseFanSlots) + "" +
            ",\"radiator\":" + ParsePartsList(cs.radiatorSlots) + "" +
            ",\"reservoir\":" + ParsePartsList(cs.reservoirSlots) + "" +
            ",\"pci\":" + ParsePartsList(cs.pciSlots) + "" +
            ",\"ram\":" + ParsePartsList(cs.ramSlots) + "" +
            ",\"m2\":" + ParsePartsList(cs.m2Slots) + "" +
            "}";
        return computerSaveString;
    }

    static string ParsePartDesc(PartDesc pd)
    {
        string result = "{" +
            "\"id\":\"" + pd.m_id + "\"" +
            ",\"name\":\"" + pd.m_partName.Trim() + "\"" +
            ",\"type\":\"" + pd.m_type.ToString().ToUpper() + "\"" +
            ",\"inShop\":" + (LevelProgression.GetLevel(CareerStatus.Get().GetKudos()) >= pd.m_levelUnlock && pd.m_inShop).ToString().ToLower() + "" +
            ",\"manufacturer\":\"" + pd.m_manufacturer + "\"" +
            ",\"price\":" + pd.m_price + "" +
            ",\"quality\":" + pd.GetQuality() + "";

        if (pd.GetType().Name == "PartDescMotherboard")
        {
            List<int> ramSpeeds = (List<int>)((FieldInfo)pd.GetType().GetField("m_memorySpeeds")).GetValue(pd);
            result = result +
                ",\"socket\":\"" + ((FieldInfo)pd.GetType().GetField("m_socket")).GetValue(pd).ToString() + "\"" +
                ",\"size\":\"" + ((FieldInfo)pd.GetType().GetField("m_size")).GetValue(pd).ToString() + "\"" +
                ",\"ddr\":\"" + ((FieldInfo)pd.GetType().GetField("m_ramType")).GetValue(pd).ToString() + "\"" +
                ",\"ramSpeed\":" + ramSpeeds.Max() + "" +
                ",\"supportSLI\":" + ((FieldInfo)pd.GetType().GetField("m_supportSLI")).GetValue(pd).ToString().ToLower() + "" +
                ",\"supportCF\":" + ((FieldInfo)pd.GetType().GetField("m_supportCrossfire")).GetValue(pd).ToString().ToLower() + "";
        }
        if (pd is PartDescCPU)
        {
            PartDescCPU cpu = (PartDescCPU)pd;
            float coefClock = (float)((FieldInfo)cpu.GetType().GetField("m_coefCoreClock", BindingFlags.NonPublic | BindingFlags.Instance)).GetValue(cpu);
            float coefMemChannel = (float)((FieldInfo)cpu.GetType().GetField("m_coefMemChannel", BindingFlags.NonPublic | BindingFlags.Instance)).GetValue(cpu);
            float coefMemClock = (float)((FieldInfo)cpu.GetType().GetField("m_coefMemClock", BindingFlags.NonPublic | BindingFlags.Instance)).GetValue(cpu);
            float coefAdjustment = (float)((FieldInfo)cpu.GetType().GetField("m_adjustment", BindingFlags.NonPublic | BindingFlags.Instance)).GetValue(cpu);

            result = result +
                ",\"wattage\":" + cpu.m_wattage + "" +
                ",\"socket\":\"" + cpu.m_socket.ToString().ToUpper() + "\"" +
                ",\"coefClock\":" + coefClock + "" +
                ",\"coefMemChannel\":" + coefMemChannel + "" +
                ",\"coefMemClock\":" + coefMemClock + "" +
                ",\"coefAdjustment\":" + coefAdjustment + "" +
                ",\"clockFreq\":" + cpu.m_freqMhz + "" +
                "";
        }
        if (pd.GetType().Name == "PartDescCooler")
        {
            object socketRequirement = ((FieldInfo)pd.GetType().GetField("m_socketRequirement")).GetValue(pd);
            object sockets = ((FieldInfo)socketRequirement.GetType().GetField("m_sockets")).GetValue(socketRequirement);
            result = result +
                ",\"sockets\":[" + ((List<CpuSocket>)sockets).Join(socket => "\"" + socket.ToString().ToUpper() + "\"", ",") + "]";
        }
        if (pd is PartDescRAM)
        {
            PartDescRAM ram = (PartDescRAM)pd;
            result = result +
                ",\"speed\":" + ram.m_speedMhz + "" +
                ",\"size\":" + ram.m_sizeGb + "" +
                ",\"ddr\":\"" + ram.m_ramType.ToString().ToUpper() + "\"";
        }
        if (pd is PartDescGPU)
        {
            PartDescGPU gpu = (PartDescGPU)pd;
            result = result +
                ",\"vram\":" + gpu.m_vramGb + "" +
                ",\"wattage\":" + gpu.m_wattage + "" +
                ",\"useSLI\":" + gpu.m_useSLI.ToString().ToLower() + "" +
                ",\"dual\":" + gpu.m_doubleGPU.ToString().ToLower() + "" +
                ",\"chipset\":\"" + gpu.m_chipSet.ToString().ToUpper() + "\"" +
                ",\"length\":" + gpu.m_length + "" +
                ",\"score11\":" + gpu.Test(gpu.m_coreClockFreq, gpu.m_memClockFreq, 1, 1) + "" +
                ",\"score21\":" + gpu.Test(gpu.m_coreClockFreq, gpu.m_memClockFreq, 2, 1) + "" +
                ",\"score12\":" + gpu.Test(gpu.m_coreClockFreq, gpu.m_memClockFreq, 1, 2) + "" +
                ",\"score22\":" + gpu.Test(gpu.m_coreClockFreq, gpu.m_memClockFreq, 2, 2) + "" +
                "";
        }
        if (pd is PartDescPSU)
        {
            PartDescPSU psu = (PartDescPSU)pd;
            result = result +
                ",\"wattage\":" + psu.m_wattage + "" +
                ",\"length\":" + psu.m_length + "" +
                ",\"size\":\"" + psu.m_size.ToString().ToUpper() + "\"" +
                "";
        }
        if (pd.GetType().Name == "PartDescStorage")
        {
            result = result +
                ",\"size\":" + (int)(((FieldInfo)pd.GetType().GetField("m_sizeGb")).GetValue(pd)) + "";
        }
        if (pd.GetType().Name == "PartDescCase")
        {
            List<MotherboardType> motherboardTypes = (List<MotherboardType>)((FieldInfo)pd.GetType().GetField("m_motherboardTypes")).GetValue(pd);
            List<PSUSize> psuSizes = (List<PSUSize>)((FieldInfo)pd.GetType().GetField("m_PSUSize")).GetValue(pd);
            result = result +
                ",\"maxPSULength\":" + ((FieldInfo)pd.GetType().GetField("m_maxPSULength")).GetValue(pd) + "" +
                ",\"maxCPUFanHeight\":" + ((FieldInfo)pd.GetType().GetField("m_maxCPUFanHeight")).GetValue(pd) + "" +
                ",\"maxGPULength\":" + ((FieldInfo)pd.GetType().GetField("m_maxGPULength")).GetValue(pd) + "" +
                ",\"maxRadiatorLength120\":" + ((FieldInfo)pd.GetType().GetField("m_maxRadiatorLength120mm")).GetValue(pd) + "" +
                ",\"maxRadiatorLength140\":" + ((FieldInfo)pd.GetType().GetField("m_maxRadiatorLength140mm")).GetValue(pd) + "" +
                ",\"psuSizes\":[" + psuSizes.Join(size => "\"" + size.ToString().ToUpper() + "\"", ",") + "]" +
                ",\"motherboardTypes\":[" + motherboardTypes.Join(type => "\"" + type.ToString().ToUpper() + "\"", ",") + "]" +
                "";
        }

        result += "}";
        return result;
    }

    static string ParsePartInstance(PartInstance part)
    {
        if (part == null)
        {
            return "null";
        }
        return "{\"id\":\"" + part.GetPartId() + "\"" +
            ",\"isNew\":" + part.IsNew().ToString().ToLower() + "" +
            ",\"isBroken\":" + part.IsBroken().ToString().ToLower() + "" +
            ",\"budgetValue\":" + part.GetBudgetValue() + "" +
            ",\"jobId\":" + part.GetJobId() + "" +
        "}";
    }

    static string ParseObjectives(List<Objective> objectives)
    {
        return "[" +
            objectives.Join(objective =>
                {
                    return "{" +
                        "\"type\":\"" + objective.GetType().ToString().ToUpper() + "\"" +
                    "}";
                }, ",")
            + "]";
    }

    static void DumpUserData()
    {
        IEnumerable<PartInstance> inventory = CareerStatus.Get().GetInventory();
        string inventoryString = "[" + inventory.Join(p => ParsePartInstance(p), ",") + "]";

        IEnumerable<Job> jobs = CareerStatus.Get().GetJobs()
            .Where(job => (new[] { Job.Status.NEW, Job.Status.READ, Job.Status.IN_TRANSIT, Job.Status.ACCEPTED }).Contains(job.GetStatus()));
        string jobString = "[" + jobs.Join(job =>
        {
            return "{" +
                "\"id\":" + job.GetId() + "" +
                ",\"from\":\"" + job.GetFrom() + "\"" +
                ",\"labor\":" + job.GetLabour() + "" +
                ",\"budget\":" + job.GetBudget() + "" +
                ",\"type\":\"" + job.GetJobType().ToString().ToUpper() + "\"" +
                ",\"initialRig\":" + ParseComputerSave(job.GetStartComputer()) + "" +
                ",\"objectives\":" + ParseObjectives(job.GetObjectives()) + "" +
            "}";
        }, ",") + "]";

        File.WriteAllText("C:/workspace/PCBSJS/pcbsjs.json", "{\"inventory\":" + inventoryString + ",\"jobs\":" + jobString + "}");
    }

    static void DumpDatabase()
    {
        string partsString = "[" + PartsDatabase.GetAllParts()
            .Join(partDesc => ParsePartDesc(partDesc), ",") +
        "]";
        File.WriteAllText("C:/workspace/PCBSJS/pcbsjs_db.json", "{\"parts\":" + partsString + "}");
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.ShowInventory))]
    class InventoryPop
    {
        static void Prefix(Case forComputer, PeripheralSlot forPeripheralSlot)
        {
        }
    }

    [HarmonyPatch(typeof(CareerStatus), nameof(CareerStatus.GetInventory))]
    class InventorySorting
    {
        static IEnumerable<PartInstance> Postfix(IEnumerable<PartInstance> value)
        {
            IEnumerable<PartInstance> result = value.ToArray();
            switch (inventorySort)
            {
                case 1:
                    result = result.Reverse();
                    break;
                case 2:
                    result = result.OrderBy(partInstance => partInstance.GetPart().m_price);
                    break;
                case 3:
                    result = result.OrderByDescending(partInstance => partInstance.GetPart().m_price);
                    break;
                case 0:
                default:
                    break;
            }
            return result;
        }
    }
}
