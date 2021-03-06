﻿// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2016 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    
// 
// Notes:
//

using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Questing.Actions;
using DaggerfallWorkshop.Game.Questing.Conditions;

namespace DaggerfallWorkshop.Game.Questing
{
    /// <summary>
    /// Hosts quests and manages their execution during play.
    /// Quests are instantiated from a source text template.
    /// It's possible to have the same quest multiple times (e.g. same fetch quest from two different mage guildhalls).
    /// Running quests can perform actions in the world (e.g. spawn enemies and play sounds).
    /// Or they can provide data to external systems like the NPC dialog interface (e.g. 'tell me about' and 'rumors').
    /// Quest support is considered to be in very early prototype stages and may change at any time.
    /// </summary>
    public class QuestMachine : MonoBehaviour
    {
        #region Fields

        const float startupDelay = 1.0f;        // How long quest machine will wait before running active quests
        const float ticksPerSecond = 10;        // How often quest machine will tick quest logic per second

        // Folder names constants
        const string questSourceFolderName = "Quests";
        const string questTablesFolderName = "Tables";

        // Table constants
        const string globalVarsTableFilename = "Quests-GlobalVars";
        const string staticMessagesTableFilename = "Quests-StaticMessages";
        const string placesTableFilename = "Quests-Places";
        const string soundsTableFilename = "Quests-Sounds";
        const string itemsTableFileName = "Quests-items";

        // Data tables
        Table globalVarsTable;
        Table staticMessagesTable;
        Table placesTable;
        Table soundsTable;
        Table itemsTable;

        List<IQuestAction> actionTemplates = new List<IQuestAction>();
        Dictionary<ulong, Quest> quests = new Dictionary<ulong, Quest>();
        List<Quest> questsToRemove = new List<Quest>();

        bool waitingForStartup = true;
        float startupTimer = 0;
        float updateTimer = 0;

        #endregion

        #region Properties

        /// <summary>
        /// Gets Quests source folder in StreamingAssets.
        /// </summary>
        public string QuestSourceFolder
        {
            get { return Path.Combine(Application.streamingAssetsPath, questSourceFolderName); }
        }

        /// <summary>
        /// Gets Tables source folder in StreamingAssets.
        /// TODO: This folder isn't ultimately exclusive to quests. Find a more generic spot later, e.g. GameManager.
        /// </summary>
        public string TablesSourceFolder
        {
            get { return Path.Combine(Application.streamingAssetsPath, questTablesFolderName); }
        }

        /// <summary>
        /// Gets the global variables data table.
        /// </summary>
        public Table GlobalVarsTable
        {
            get { return globalVarsTable; }
        }

        /// <summary>
        /// Gets the static message names data table.
        /// </summary>
        public Table StaticMessagesTable
        {
            get { return staticMessagesTable; }
        }

        /// <summary>
        /// Gets the places data table.
        /// </summary>
        public Table PlacesTable
        {
            get { return placesTable; }
        }

        /// <summary>
        /// Gets the sounds data table.
        /// </summary>
        public Table SoundsTable
        {
            get { return soundsTable; }
        }

        /// <summary>
        /// Gets the items data table.
        /// </summary>
        public Table ItemsTable
        {
            get { return itemsTable; }
        }

        #endregion

        #region Unity

        void Awake()
        {
            SetupSingleton();

            globalVarsTable = new Table(Instance.GetTableSourceText(globalVarsTableFilename));
            staticMessagesTable = new Table(Instance.GetTableSourceText(staticMessagesTableFilename));
            placesTable = new Table(Instance.GetTableSourceText(placesTableFilename));
            soundsTable = new Table(Instance.GetTableSourceText(soundsTableFilename));
            itemsTable = new Table(Instance.GetTableSourceText(itemsTableFileName));
        }

        void Start()
        {
            RegisterActionTemplates();
        }

        private void Update()
        {
            // Handle startup delay
            if (waitingForStartup)
            {
                startupTimer += Time.deltaTime;
                if (startupTimer < startupDelay)
                    return;
                waitingForStartup = false;
            }

            // Increment update timer
            updateTimer += Time.deltaTime;
            if (updateTimer < (1f / ticksPerSecond))
                return;

            // Update quests
            questsToRemove.Clear();
            foreach (Quest quest in quests.Values)
            {
                quest.Update();
                if (quest.QuestComplete)
                    questsToRemove.Add(quest);
            }

            // Remove completed quests after update completed
            foreach (Quest quest in questsToRemove)
            {
                quests.Remove(quest.UID);
            }

            // Reset update timer
            updateTimer = 0;
        }

        #endregion

        #region Action Methods

        /// <summary>
        /// All actions must be registered here so they can be evaluated and factoried at runtime.
        /// If an action pattern match cannot be found that action will just be ignored by quest system.
        /// The goal is to add incremental action support over time until 100% compatibility is reached.
        /// </summary>
        void RegisterActionTemplates()
        {
            // Register example actions
            RegisterAction(new JuggleAction(null));

            // Register default conditions
            RegisterAction(new WhenTask(null));

            // Register default actions
            RegisterAction(new EndQuest(null));
            RegisterAction(new Prompt(null));
            RegisterAction(new Say(null));
            RegisterAction(new PlaySound(null));
            RegisterAction(new StartTask(null));
            RegisterAction(new ClearTask(null));
            RegisterAction(new LogMessage(null));
            RegisterAction(new PickRandomTask(null));
            RegisterAction(new StartClock(null));
            RegisterAction(new StopClock(null));
            RegisterAction(new RemoveLogMessage(null));
            RegisterAction(new PlayVideo(null));
        }

        void RegisterAction(IQuestAction actionTemplate)
        {
            actionTemplates.Add(actionTemplate);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempts to load quest source text from StreamingAssets/Quests.
        /// </summary>
        /// <param name="questName">Quest filename. Extension .txt is optional.</param>
        /// <returns>Array of lines in quest text, or empty array.</returns>
        public string[] GetQuestSourceText(string questName)
        {
            string[] source = new string[0];

            // Append extension if not present
            if (!questName.EndsWith(".txt"))
                questName += ".txt";

            // Attempt to load quest source file
            string path = Path.Combine(QuestSourceFolder, questName);
            if (!File.Exists(path))
            {
                Debug.LogErrorFormat("Quest filename path {0} not found.", path);
            }
            else
            {
                source = File.ReadAllLines(path);
            }

            return source;
        }

        /// <summary>
        /// Attempts to load table text from StreamingAssets/Tables.
        /// TODO: Tables are ultimately not exclusive to quests. Relocate this later.
        /// </summary>
        /// <param name="tableName">Table filename. Extension .txt is optional.</param>
        /// <returns>Array of lines in table text, or empty array.</returns>
        public string[] GetTableSourceText(string tableName)
        {
            string[] table = new string[0];

            // Append extension if not present
            if (!tableName.EndsWith(".txt"))
                tableName += ".txt";

            // Attempt to load quest source file
            string path = Path.Combine(TablesSourceFolder, tableName);
            if (!File.Exists(path))
            {
                Debug.LogErrorFormat("Table filename path {0} not found.", path);
            }
            else
            {
                table = File.ReadAllLines(path);
            }

            return table;
        }

        /// <summary>
        /// Returns a list of all active log messages from all active quests
        /// </summary>
        /// <returns>List of log messages</returns>
        public List<Message> GetAllQuestLogMessages()
        {
            List<Message> questMessages = new List<Message>();

            foreach (var quest in quests.Values)
            {
                var logEntries = quest.GetLogMessages();

                foreach (var logEntry in logEntries)
                {
                    var message = quest.GetMessage(logEntry.messageID);
                    if (message != null)
                        questMessages.Add(message);
                }
            }

            return questMessages;
        }

        /// <summary>
        /// Instantiate a new quest from name.
        /// Quest will attempt to load from QuestSourceFolder property path.
        /// </summary>
        /// <param name="questName">Name of quest filename. Extensions .txt is optional.</param>
        /// <returns>Quest object if successfully parsed, otherwise null.</returns>
        public Quest InstantiateQuest(string questName)
        {
            // Load quest source
            string[] source = GetQuestSourceText(questName);
            if (source == null || source.Length == 0)
                return null;

            return InstantiateQuest(source);
        }

        /// <summary>
        /// Instantiate a new quest from source text array.
        /// </summary>
        /// <param name="questSource">Array of lines from quuest source file.</param>
        /// <returns>Quest.</returns>
        public Quest InstantiateQuest(string[] questSource)
        {
            Parser parser = new Parser();
            Quest quest = parser.Parse(questSource);
            if (quest != null)
            {
                quests.Add(quest.UID, quest);
            }

            return quest;
        }

        /// <summary>
        /// Find registered action template based on source line.
        /// </summary>
        /// <param name="source">Action source line.</param>
        /// <returns>IQuestAction template.</returns>
        public IQuestAction GetActionTemplate(string source)
        {
            // Brute force check every registered action for now
            // Would like a more elegant way of accomplishing this
            foreach(IQuestAction action in actionTemplates)
            {
                if (action.Test(source).Success)
                    return action;
            }

            // No pattern match found
            return null;
        }

        #endregion

        #region Private Methods
        #endregion

        #region Singleton

        static QuestMachine instance = null;
        public static QuestMachine Instance
        {
            get
            {
                if (instance == null)
                {
                    if (!FindQuestMachine(out instance))
                    {
                        GameObject go = new GameObject();
                        go.name = "QuestMachine";
                        instance = go.AddComponent<QuestMachine>();
                    }
                }
                return instance;
            }
        }

        public static bool HasInstance
        {
            get
            {
                return (instance != null);
            }
        }

        public static bool FindQuestMachine(out QuestMachine questMachineOut)
        {
            questMachineOut = GameObject.FindObjectOfType(typeof(QuestMachine)) as QuestMachine;
            if (questMachineOut == null)
            {
                DaggerfallUnity.LogMessage("Could not locate QuestMachine GameObject instance in scene!", true);
                return false;
            }

            return true;
        }

        private void SetupSingleton()
        {
            if (instance == null)
                instance = this;
            else if (instance != this)
            {
                if (Application.isPlaying)
                {
                    DaggerfallUnity.LogMessage("Multiple QuestMachine instances detected in scene!", true);
                    Destroy(gameObject);
                }
            }
        }

        #endregion
    }
}