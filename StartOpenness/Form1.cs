using Microsoft.Win32;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Settings;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;


namespace StartOpenness
{
    public partial class Form1 : Form //aqui programamos toda la funcionalidad de la interfaz. 
    {

        public Form1(string[] args)
        {
            InitializeComponent();
            AppDomain CurrentDomain = AppDomain.CurrentDomain;
            CurrentDomain.AssemblyResolve += new ResolveEventHandler(MyResolver);
        }


        //+++++++++++++ Metodos creados +++++++++++++++++++

        //Assembly para conectar
        private static Assembly MyResolver(object sender, ResolveEventArgs args)
        {
            int index = args.Name.IndexOf(',');
            if (index == -1)
            {
                return null;
            }
            string name = args.Name.Substring(0, index);

            RegistryKey filePathReg = Registry.LocalMachine.OpenSubKey(
                "SOFTWARE\\Siemens\\Automation\\Openness\\15.1\\PublicAPI\\15.1.0.0");

            if (filePathReg == null)
                return null;

            object oRegKeyValue = filePathReg.GetValue(name);
            if (oRegKeyValue != null)
            {
                string filePath = oRegKeyValue.ToString();

                string path = filePath;
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return Assembly.LoadFrom(fullPath);
                }
            }

            return null;
        }


        public void StartTIA(object sender, EventArgs e)
        {
            try
            {
                if (rdb_WithoutUI.Checked == true) //radioButton ohne User Interface
                {
                    MyTiaPortal = new TiaPortal(TiaPortalMode.WithoutUserInterface);
                    txt_Status.Text = "TIA Portal started without user interface";
                    _tiaProcess = TiaPortal.GetProcesses()[0];
                }

                else  //radioButton mit User Interface
                {

                    MyTiaPortal = new TiaPortal(TiaPortalMode.WithUserInterface);
                    ProjectComposition projects = MyTiaPortal.Projects; //tenemos composicion de proyectos. Podemos tener mas de uno.
                    txt_Status.Text = "TIA Portal started with user interface";
                    project = null;//inicializamos que aun no ha sido posible abrir el programa 
                    project = projects.Open(projectPath); //usamos sentenci try, porque lo que tenemos entre estos corchetes puede suceder o no. Para que no caiga el programa, lo programamos asi

                }
            }
            catch (Exception)
            {
                txt_Status.Text = String.Format("Could not open project {0}, try other thing", projectPath.FullName); //Sacamos por pantalla el nombre completo del path donde se encuentra el programa

                return;
            }

            txt_Status.Text = "TIA Portal started with user interface"; // visualizacion en la interfaz de que se abre con/sin interfaz
            btn_SearchProject.Enabled = true;
            btn_Dispose.Enabled = true;
            btn_Start.Enabled = false;

        }


        public void DisposeTIA(object sender, EventArgs e) //Close TIA. 
        {
            IList<TiaPortalProcess> tiaProcessList = TiaPortal.GetProcesses(); //Obtenemos cuantos procesos de TIA hay abiertos en un momento

            if (tiaProcessList.Count > 0) //si hay mas de un proceso abierto en TIA. 
            {
                //Show YES/NO warning that says that ALL the TIA Portal 15.1 instances will be closed

                DialogResult dialogResult = MessageBox.Show(closeAllInstances, closeAllInstancesWarning, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (dialogResult == DialogResult.Yes)
                {
                    //Update status message
                    txt_Status.Text = "TIA Portal closing";

                    //Disable all the buttons except "Dispose Project"
                    btn_Start.Enabled = false;
                    btn_Dispose.Enabled = true;
                    btn_CloseProject.Enabled = false;
                    btn_SearchProject.Enabled = false;
                    btn_CompileHW.Enabled = false;
                    btn_SaveProject.Enabled = false;
                    MyTiaPortal.Dispose();

                }
                else
                {
                    txt_Status.Text = "TIA Portal are not going to close";
                    //Disable all the buttons except 
                    btn_Start.Enabled = false;
                    btn_Dispose.Enabled = false;
                    btn_CloseProject.Enabled = false;
                    btn_SearchProject.Enabled = false;
                    btn_CompileHW.Enabled = false;
                    btn_SaveProject.Enabled = false;

                }
            }


        }

        public void SearchProject(object sender, EventArgs e)
        {
            //Abrimos la busqueda de projectos que tengan las terminaciones 15_1. 

            OpenFileDialog fileSearch = new OpenFileDialog();

            fileSearch.Filter = "*.ap15_1|*.ap15_1";
            fileSearch.RestoreDirectory = true;
            fileSearch.ShowDialog();

            string ProjectPath = fileSearch.FileName.ToString();

            if (string.IsNullOrEmpty(ProjectPath) == false)
            {
                OpenProject(ProjectPath);
            }
        }

        public void OpenProject(string ProjectPath)
        {
            try
            {
                MyProject = MyTiaPortal.Projects.Open(new FileInfo(ProjectPath));
                OpenedProject = true;
                txt_Status.Text = "Project " + ProjectPath + " opened";

            }
            catch (Exception ex)
            {
                txt_Status.Text = "Error while opening project" + ex.Message;
                OpenedProject = false;
            }

            btn_CompileHW.Enabled = true;
            btn_CloseProject.Enabled = true;
            btn_SearchProject.Enabled = false;
            btn_SaveProject.Enabled = true;
            btn_AddHW.Enabled = true;
        }

        public void SaveProject(object sender, EventArgs e)
        {
            MyProject.Save();
            txt_Status.Text = "Project saved";
        }

        public void CloseProject(object sender, EventArgs e) //cerrar instancia del proyecto. 
        {
            MyProject.Close();

            txt_Status.Text = "Project closed";

            btn_SearchProject.Enabled = true;
            btn_CloseProject.Enabled = false;
            btn_SaveProject.Enabled = false;
            btn_CompileHW.Enabled = false;


        }

        //Cerramos las instancias del TIA abiertas
        private void CloseTIAInstance(object sender, EventArgs e)
        {
            TIAClosed = false;

            //Try to close a project if it is opened
            try
            {
                //Close project if it is opened
                CloseProject(sender, e);
            }
            catch
            {

            }
            //Desconectamos del TIA Portal. 
            MyTiaPortal.Dispose();
            //Cerramos todas las instancias de TIA Portal abiertas
            foreach (var process in Process.GetProcessesByName("Siemens.Automation.Portal"))
            {
                process.Kill();
            }
            //Indicamos que el TIA se ha cerrado. 

            TIAClosed = true;
        }
        public void Compile(object sender, EventArgs e)
        {
            btn_CompileHW.Enabled = false;
            string devname = txt_AddDevice.Text + txt_OrderNo.Text; //es el nombre del device con el SPS Name y su Type. 
            bool found = false; //que no exista antes en el proyecto. 

            foreach (Device device in MyProject.Devices)
            {
                DeviceItemComposition deviceItemAggregation = device.DeviceItems;
                foreach (DeviceItem deviceItem in deviceItemAggregation)
                {
                    if (deviceItem.Name == devname || device.Name == devname)
                    {
                        SoftwareContainer softwareContainer = deviceItem.GetService<SoftwareContainer>();
                        if (softwareContainer != null)
                        {
                            if (softwareContainer.Software is PlcSoftware)
                            {
                                PlcSoftware controllerTarget = softwareContainer.Software as PlcSoftware; //Direccionar el software, es que estamos dentro de los "Programmbaustein"
                                if (controllerTarget != null) //si el PLC que buscamos tiene un software no nulo
                                {
                                    found = true; //se ha encontrado el PLC que queremos runear.
                                    ICompilable compiler = controllerTarget.GetService<ICompilable>(); //Compilamos una vez el PLC que queremos.
                                    CompilerResult result = compiler.Compile(); //guardamos los datos de la compilación
                                    txt_Status.Text = "Compiling of " + controllerTarget.Name + ": State: " + result.State + " / Warning Count: " + result.WarningCount + " / Error Count: " + result.ErrorCount;

                                }
                            }
                            if (softwareContainer.Software is HmiTarget) //Si queremos compilar un HMI: 
                            {
                                HmiTarget hmitarget = softwareContainer.Software as HmiTarget;
                                if (hmitarget != null)
                                {
                                    found = true;
                                    ICompilable compiler = hmitarget.GetService<ICompilable>();
                                    CompilerResult result = compiler.Compile();
                                    txt_Status.Text = "Compiling of " + hmitarget.Name + ": State: " + result.State + " / Warning Count: " + result.WarningCount + " / Error Count: " + result.ErrorCount;
                                }

                            }
                        }
                    }
                }
            }
            if (found == false) //no se ha encontrado ningun dispositivo con ese nombre, por ello no se puede compilar. 
            {
                txt_Status.Text = "Found no device with name " + txt_AddDevice.Text + txt_OrderNo.Text;
            }

            btn_CompileHW.Enabled = true;
        }

        public void ReadPLCStructure(PlcSoftware controllerTarget)
        {

            //Read PLC blocks
            FindBlocks(controllerTarget.BlockGroup, "");

        }

        public void btn_AddHW_Click(object sender, EventArgs e) //Anadimos al proyecto un SPS que queramos
        {
            btn_AddHW.Enabled = false;

            btn_AddHW.Enabled = true;

        }

        public void btn_ConnectTIA(object sender, EventArgs e) //boton de conectar el projecto con el TIA Portal. 
        {
            btn_Connect.Enabled = false;
            IList<TiaPortalProcess> processes = TiaPortal.GetProcesses();
            switch (processes.Count)
            {
                case 1: //si solo tenemos un proceso runeando
                    _tiaProcess = processes[0];
                    MyTiaPortal = _tiaProcess.Attach();

                    if (MyTiaPortal.GetCurrentProcess().Mode == TiaPortalMode.WithUserInterface)
                    {
                        rdb_WithUI.Checked = true;
                    }
                    else
                    {
                        rdb_WithoutUI.Checked = true;
                    }


                    if (MyTiaPortal.Projects.Count <= 0)
                    {
                        txt_Status.Text = "No TIA Portal Project was found!";
                        btn_Connect.Enabled = true;
                        return;
                    }
                    MyProject = MyTiaPortal.Projects[0];
                    break;
                case 0:
                    txt_Status.Text = "No running instance of TIA Portal was found!";
                    btn_Connect.Enabled = true;
                    return;
                default:
                    txt_Status.Text = "More than one running instance of TIA Portal was found!";
                    btn_Connect.Enabled = true;
                    return;
            }
            txt_Status.Text = _tiaProcess.ProjectPath.ToString();
            btn_Start.Enabled = false;
            btn_Connect.Enabled = true;
            btn_Dispose.Enabled = true;
            btn_CompileHW.Enabled = true;
            btn_CloseProject.Enabled = true;
            btn_SearchProject.Enabled = false;
            btn_SaveProject.Enabled = true;
            btn_AddHW.Enabled = true;
        }

        private void rdb_WithUI_CheckedChanged(object sender, EventArgs e)
        {
            //Volvemos a ver que quiere el usuario. 

            StartTIA(sender, e);
        }

        private void grp_TIA_Enter(object sender, EventArgs e)
        {
        }

        private void label4_Click(object sender, EventArgs e)
        {
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {
        }

        private void txt_AddDevice_TextChanged(object sender, EventArgs e)
        {
            searchPLCs(); //Buscamos si hay algun PLC con este nombre.
        }
        private void txt_OrderNo_TextChanged(object sender, EventArgs e)
        {
            //comprobar que el numero es correcto es correcta
        }

        private void txt_Version_TextChanged(object sender, EventArgs e)
        {
            //comprobar que la version es correcta
        }

        private void txt_DeviceName_TextChanged(object sender, EventArgs e)
        {
            //comprobar que ese nombre no está ya en el projecto abierto. 
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }


        private void grp_Compile_Enter(object sender, EventArgs e)
        {

        }

        private void label6_Click(object sender, EventArgs e)
        {

        }

        //buscar PLC y si no existe el nombre, crea el dispositivo. 

        public void searchPLCs()
        {
            string MLFB = "OrderNumber:" + txt_OrderNo.Text + "/" + txt_Version.Text; //tipo y version introducido por pantalla
            string name = txt_AddDevice.Text; //nombre del PLC (nombre de la instancia que tendrá dentro del programa)
            string devname = "station" + txt_AddDevice.Text;
            bool exist = false;

            foreach (Device device in MyProject.Devices)
            {
                DeviceItemComposition deviceItemAggregation = device.DeviceItems;
                foreach (DeviceItem deviceItem in deviceItemAggregation)
                {
                    if (deviceItem.Name == devname || device.Name == devname)
                    {
                        SoftwareContainer softwareContainer = deviceItem.GetService<SoftwareContainer>();
                        if (softwareContainer != null)
                        {
                            if (softwareContainer.Software is PlcSoftware)
                            {
                                PlcSoftware controllerTarget = softwareContainer.Software as PlcSoftware;
                                if (controllerTarget != null)
                                {
                                    exist = true;

                                }
                            }
                            if (softwareContainer.Software is HmiTarget)
                            {
                                HmiTarget hmitarget = softwareContainer.Software as HmiTarget;
                                if (hmitarget != null)
                                {
                                    exist = true;

                                }

                            }
                        }
                    }
                }
            }
            if (exist == true) //Hay un dispositivo con el mismo nombre. Se debe de volver a introducir los datos
            {
                txt_Status.Text = "Device " + txt_AddDevice.Text + txt_OrderNo.Text + " already exists, introduce other value";
            }
            else
            {
                Device deviceName = MyProject.Devices.CreateWithItem(MLFB, name, devname); //Creamos en el proyecto directamente un sps 

                txt_Status.Text = "Add Device Name: " + name + " with Order Number: " + txt_OrderNo.Text + " and Firmware Version: " + txt_Version.Text;
            }

            //Loop for all the devices in the project
            foreach (Device device in project.Devices) //por cada device que haya dentro del projecto, llamado "project"
            {
                DeviceItemComposition deviceItemAggregation = device.DeviceItems;

                //Loop for all the device items in the device

                foreach (DeviceItem deviceItem in deviceItemAggregation)
                {
                    SoftwareContainer softwareContainer = deviceItem.GetService<SoftwareContainer>();

                }
            }
        }

        //crear la jerarquia de carpetas dentro del proyecto. 
        public void doProccessHierarchy()
        {

            //Bucle para los devices del proyecto(por cada device que tengamos dentro del proyecto ya abierto, que direccionamos con el nombre "project") 
            foreach (Device device in project.Devices)
            {
                DeviceItemComposition deviceItemAggregation = device.DeviceItems;

                //Dentro de un PLC, que mas disposiivos tenemos. 

                foreach (DeviceItem deviceItem in deviceItemAggregation)
                {
                    //De cada uno obtenemos su software determinado

                    SoftwareContainer softwareContainer = deviceItem.GetService<SoftwareContainer>();

                    if (softwareContainer != null) //si para ese, el software es diferente a nulo 
                    {
                        //If it is a PLC
                        if (softwareContainer.Software is PlcSoftware) //si lo que tenemos en este software es de PLC
                        {
                            PlcSoftware controllerTarget = softwareContainer.Software as PlcSoftware; //guardamos en la bariable controllerTarget 

                            if (controllerTarget != null) //si el software que tenemos es diferente a nulo 
                            {
                                //If the PLC found is the same as the one selected

                                if (controllerTarget.Name == selectedPLC) //si el nombre del software es el que vamos buscando del PLC
                                {

                                    //Read structure of the PLC
                                    ReadPLCStructure(controllerTarget);



                                }
                            }
                        }
                    }
                }
            }


        }

        //Exporta un bloque regular. Le pasamos el bloque en el que estemos dentro del sft. Aqui entran FB, FC, OB, DB y los lenguajes: AWL, FUP, KOP; Graph,scl
        public bool ExportRegularBlock(PlcBlock plcBlock, string blockPath) //le indicamos que bloque, y el path donde lo exportamos
        {
            //Variable that indicates if the process succeeds
            bool exportSucced;
            //Errors that could occur

            //Define complete path for the XML file to be created
            string corrBlockName = plcBlock.Name.Replace(@"\", "__SLIDE__");
            //Replace character / 
            corrBlockName = corrBlockName.Replace("/", "__RSLIDE__");
            //Replace characters > and <
            corrBlockName = corrBlockName.Replace(">", "__GREATER__");
            corrBlockName = corrBlockName.Replace("<", "__LOWER__");

            //Define path for the XML
            string pathDest = exportXMLPath + @"\" + corrBlockName + ".xml";
            FileInfo path = new FileInfo(pathDest);

            try
            {
                //    PlcBlock plcBlock = plcSoftware.BlockGroup.Blocks.Find("FC_Contador");
                //    plcBlock.Export(new FileInfo(string.Format(@"C:\Users\lm86229\Documents\Automatisierung\FC_Contador.xml", plcBlock.Name)), ExportOptions.WithDefaults);

                plcBlock.Export(path, ExportOptions.WithDefaults);
                exportSucced = true;
            }

            catch
            {
                //Exportation has failed
                exportSucced = false;
                //Variable that indicates if some plcBlock couldn´t be exported is set to true
                blocksNotExported = true;

                string blockName = plcBlock.Name;
                string blockType = plcBlock.GetType().Name;
                double blockNumber = plcBlock.Number;

                //Modify escape characters
                blockName = blockName.Replace("<", "&lt;");
                blockName = blockName.Replace(">", "&gt;");
                blockName = blockName.Replace("&", "&amp;");
                blockName = blockName.Replace("\"", "&quot;");
                blockName = blockName.Replace("`", "&apos;");
            }

            return exportSucced;

        }
        //Exportar tipos de datos. 
        public bool ExportDataType(PlcType plcType)
        {
            //Variable that indicates if the process succeeds
            bool exportSucced;
            //Define complete path for the XML file to be created
            string corrBlockName = plcType.Name.Replace(@"\", "__SLIDE__");
            //Replace character / 
            corrBlockName = corrBlockName.Replace("/", "__RSLIDE__");
            //Replace character >
            corrBlockName = corrBlockName.Replace(">", "__GREATER__");
            //Replace character <
            corrBlockName = corrBlockName.Replace("<", "__LOWER__");

            //Define path for the XML
            string pathDest = exportXMLPath + @"\" + corrBlockName + ".xml";

            FileInfo path = new FileInfo(pathDest);


            //Try to export the plcBlock
            try
            {
                //Export plcBlock
                plcType.Export(path, ExportOptions.WithDefaults);
                //Exportation has succeeded
                exportSucced = true;
            }
            //If it does not work
            catch (Exception e)
            {
                Console.WriteLine(e.ToString()); //scamos por pantalla que ha ocurrido 
                exportSucced = false;
            }

            return exportSucced;
        }
        //Buscar un bloque en concreto
        public void FindBlocks(PlcBlockGroup plcBlockGroup, string obSearchingPath)
        {

            //Variable to count the number of OBs
            int number_OBs = 0;
            //Variable in which will be saved whether the exportation of a block successfull is or not
            bool exportSucceed = false;

            //Miramos los datos en la carpeta actual. 

            foreach (PlcBlock block in plcBlockGroup.Blocks)
            {

                //If the block is not a global data block
                if (block.GetType().Name != "GlobalDB")
                {
                    //Try to export the block and save the result
                    exportSucceed = ExportRegularBlock(block, obSearchingPath);
                }
                //If the exportation was successfull
                if (exportSucceed)
                {
                    //If the block is an OB
                    if (block.GetType().Name == "OB")
                    {
                        //Increase the number of OBs
                        number_OBs++;
                    }


                }
                //Set the variable again to false for the next loop
                exportSucceed = false;

            }
            //Miramos dentro de carpetas y subcarpetas de nuestro proyecto 

            foreach (PlcBlockUserGroup folder in plcBlockGroup.Groups)
            {
                //Call recursively the same function with a subfolder
                FindBlocks(plcBlockGroup.Groups.Find(folder.Name), obSearchingPath + @"\" + folder.Name);
            }
        }
        //Buscar un tipo de dato 
        public void FindDataTypes(PlcTypeGroup plcDataType, string obSearchingPath)
        {

            //Export all the data types in the actual folder
            foreach (PlcType type in plcDataType.Types)
            {
                bool exportSucceed = false;
                int exportedTypes = 0;


                //Try to export the data type and save the result
                exportSucceed = ExportDataType(type);


                //If the exportation was successfull
                if (exportSucceed)
                {
                    exportedTypes++;
                }
                //Set the variable again to false for the next loop
                exportSucceed = false;

            }
            //Explore all the folders in the actual folder to look for data types and subfolders
            foreach (PlcTypeGroup folder in plcDataType.Groups)
            {
                //Call recursively the same function with a subfolder
                FindDataTypes(plcDataType.Groups.Find(folder.Name), obSearchingPath + @"\" + folder.Name);
            }
        }
        //Dar la opcion de buscar en el proyecto 
        public static void SetSearchInPoject(Project project, TiaPortal tiaPortal)
        {
            TiaPortalSettingsFolder generalSettingsFolder = tiaPortal.SettingsFolders.Find("General");
            TiaPortalSetting searchSetting = generalSettingsFolder.Settings.Find("SearchInProject");

            if (((bool)searchSetting.Value))
            {
                searchSetting.Value = false;
            }
        }

        //Create temporary folders
        public void CreateFolders()
        {

            //Create folder to export all the XML files
            Directory.CreateDirectory(exportXMLPath);
        }



        //Variables usadas

        static string exeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        static string workPath = System.IO.Path.GetDirectoryName(exeFilePath);
        static string relativeTempPath = "Temp";
        static string tempPath = System.IO.Path.Combine(workPath, relativeTempPath);
        static string relativePathExports = "Exports_XML";
        public string exportXMLPath = System.IO.Path.Combine(tempPath, relativePathExports);
        public static Project project { get; set; }
        public static bool OpenedProject;
        public static Device FindPlc { get; set; }
        public static bool blocksNotExported;
        public string selectedPLC;
        public string plcName = " "; //lo inicializamos a vacio, hasta que no hagamos una vuelta en el programa
        FileInfo projectPath = new FileInfo(@"C:\Users\lm86229\Documents\Automatisierung\Projekt1_pruebaTIAOpeness\Projekt1_pruebaTIAOpeness.ap15_1"); //programa de prueba
        public static TiaPortalProcess _tiaProcess;
        string closeAllInstances = "All TIA Portal instances will be closed. Do you want to continue?";
        string closeAllInstancesWarning = "Close all TIA Portal instances";
        public bool TIAClosed;

        //Listas donde guardaremos datos del projecto abierto  (ordenado por jerarquia) 

        List<PlcSoftware> plcs = new List<PlcSoftware>(); //Guardamos los PLC´s que tengamos en un proyecto.
        List<PlcBlock> blocksBlock = new List<PlcBlock>(); //Guardamos los bloques con sus bases de datos.
        List<CodeBlock> blocks = new List<CodeBlock>();//Guardamos los bloques del proyecto actual (sin base de datos) 
        List<DataBlock> dataBlocks = new List<DataBlock>(); //Guardamos los DB de cada uno

        //Prueba exportar elementos
        public PlcSoftware controllerTarget;
        public PlcBlock plcblock;


        public TiaPortal MyTiaPortal { get; set; }
        public Project MyProject { get; set; }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void label8_Click(object sender, EventArgs e)
        {

        }

        private void label9_Click(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e) //button export click 
        {

            //    string extPath = @"C:\Users\lm86229\Desktop";
            //    selectedPLC = "PLC_1"; 
            //    //Bucle para los devices del proyecto(por cada device que tengamos dentro del proyecto ya abierto, que direccionamos con el nombre "project") 
            //    foreach (Device device in project.Devices)
            //    {
            //        DeviceItemComposition deviceItemAggregation = device.DeviceItems;

            //        //Dentro de un PLC, que mas disposiivos tenemos. 

            //        foreach (DeviceItem deviceItem in deviceItemAggregation)
            //        {
            //            //De cada uno obtenemos su software determinado

            //            SoftwareContainer softwareContainer = deviceItem.GetService<SoftwareContainer>();

            //            if (softwareContainer != null) //si para ese, el software es diferente a nulo 
            //            {
            //                //If it is a PLC
            //                if (softwareContainer.Software is PlcSoftware) //si lo que tenemos en este software es de PLC
            //                {
            //                    PlcSoftware controllerTarget = softwareContainer.Software as PlcSoftware; //guardamos en la bariable controllerTarget 

            //                    if (controllerTarget != null) //si el software que tenemos es diferente a nulo 
            //                    {
            //                        //If the PLC found is the same as the one selected

            //                        if (controllerTarget.Name == selectedPLC) //si el nombre del software es el que vamos buscando del PLC
            //                        {
            //                            if(controllerTarget.BlockGroup.Blocks.CreateFB)




            //                        }
            //                    }
            //                }
            //            }
            //        }
            //    }


            //}
            bool creado = true;
            int valor_FC = 4;
            ProgrammingLanguage leng = ProgrammingLanguage.LAD;
            controllerTarget.BlockGroup.Blocks.CreateFB("FC_Contador", creado, valor_FC, leng);
        }
    }

}

