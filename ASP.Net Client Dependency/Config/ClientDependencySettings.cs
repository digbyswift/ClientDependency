﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Configuration;
using System.Configuration.Provider;
using System.IO;
using System.Web;
using System.Configuration;
using ClientDependency.Core.CompositeFiles;
using ClientDependency.Core.FileRegistration.Providers;
using ClientDependency.Core.CompositeFiles.Providers;
using ClientDependency.Core.Logging;
using ClientDependency.Core.Mvc.Providers;

namespace ClientDependency.Core.Config
{
    public class ClientDependencySettings
    {

        private ClientDependencySettings()
        {
            LoadProviders();
        }

        /// <summary>
        /// Singleton
        /// </summary>
        public static ClientDependencySettings Instance
        {
            get
            {
                return m_Settings;
            }
        }

        private static readonly ClientDependencySettings m_Settings = new ClientDependencySettings();

        private object m_Lock = new object();

        private WebFormsFileRegistrationProvider m_FileRegisterProvider = null;
        private FileRegistrationProviderCollection m_FileRegisterProviders = null;

        private BaseCompositeFileProcessingProvider m_CompositeFileProvider = null;
        private CompositeFileProcessingProviderCollection m_CompositeFileProviders = null;

        private BaseRenderer m_MvcRenderer = null;
        private MvcRendererCollection m_MvcRenderers = null; 

        /// <summary>
        /// The file extensions of Client Dependencies that are file based as opposed to request based.
        /// Any file that doesn't have the extensions listed here will be request based, request based is
        /// more overhead for the server to process.
        /// </summary>
        /// <example>
        /// A request based JavaScript file may be  a .ashx that dynamically creates JavaScript server side.
        /// </example>
        /// <remarks>
        /// If this is not explicitly set, then the extensions 'js' and 'css' are the defaults.
        /// </remarks>
        public List<string> FileBasedDependencyExtensionList { get; set; }

        
        //public bool EnableCompositeFiles { get; set; }
        
        public int Version { get; set; }

        private ILogger _logger;
        public ILogger Logger
        {
            get
            {
                return _logger;
            }
        }
        public BaseRenderer DefaultMvcRenderer
        {
            get
            {
                return m_MvcRenderer;
            }
        }
        public MvcRendererCollection MvcRendererCollection
        {
            get
            {
                return m_MvcRenderers;
            }
        }
        public WebFormsFileRegistrationProvider DefaultFileRegistrationProvider
        {
            get
            {
                return m_FileRegisterProvider;
            }
        }
        public FileRegistrationProviderCollection FileRegistrationProviderCollection
        {
            get
            {
                return m_FileRegisterProviders;
            }
        }
        public BaseCompositeFileProcessingProvider DefaultCompositeFileProcessingProvider
        {
            get
            {
                return m_CompositeFileProvider;
            }
        }
        public CompositeFileProcessingProviderCollection CompositeFileProcessingProviderCollection
        {
            get
            {
                return m_CompositeFileProviders;
            }
        }

        public ClientDependencySection ConfigSection { get; private set; }
       
        public string CompositeFileHandlerPath { get; set; }
       
        private void LoadProviders()
        {
            if (m_FileRegisterProvider == null)
            {
                lock (m_Lock)
                {
                    // Do this again to make sure _provider is still null
                    if (m_FileRegisterProvider == null)
                    {
                        ConfigSection = (ClientDependencySection)ConfigurationManager.GetSection("clientDependency");

                        m_FileRegisterProviders = new FileRegistrationProviderCollection();
                        m_CompositeFileProviders = new CompositeFileProcessingProviderCollection();
                        m_MvcRenderers = new MvcRendererCollection();

                        // if there is no section found, then create one
                        if (ConfigSection == null)
                        {
                            //create a new section with the default settings
                            ConfigSection = new ClientDependencySection();                            
                        }

                        //load the providers from the config, if there isn't config sections then add default providers
                        LoadDefaultCompositeFileConfig(ConfigSection);
                        LoadDefaultMvcFileConfig(ConfigSection);
                        LoadDefaultFileRegConfig(ConfigSection);

                        //set the defaults

                        m_FileRegisterProvider = m_FileRegisterProviders[ConfigSection.FileRegistrationElement.DefaultProvider];
                        if (m_FileRegisterProvider == null)
                            throw new ProviderException("Unable to load default file registration provider");

                        m_CompositeFileProvider = m_CompositeFileProviders[ConfigSection.CompositeFileElement.DefaultProvider];
                        if (m_CompositeFileProvider == null)
                            throw new ProviderException("Unable to load default composite file provider");

                        m_MvcRenderer = m_MvcRenderers[ConfigSection.MvcElement.DefaultRenderer];
                        if (m_MvcRenderer == null)
                            throw new ProviderException("Unable to load default mvc renderer");

                        CompositeFileHandlerPath = ConfigSection.CompositeFileElement.CompositeFileHandlerPath;                        

                        this.Version = ConfigSection.Version;

                        FileBasedDependencyExtensionList = ConfigSection.FileRegistrationElement.FileBasedDependencyExtensionList.ToList();


                        if (string.IsNullOrEmpty(ConfigSection.LoggerType))
                        {
                            _logger = new NullLogger();
                        }
                        else
                        {
                            var t = Type.GetType(ConfigSection.LoggerType);
                            if (!typeof(ILogger).IsAssignableFrom(t))
                            {
                                throw new ArgumentException("The loggerType '" + ConfigSection.LoggerType + "' does not inherit from ClientDependency.Core.Logging.ILogger");
                            }

                            _logger = (ILogger)Activator.CreateInstance(t);
                        }
                    }
                }
            }
        }

        private void LoadDefaultFileRegConfig(ClientDependencySection section)
        {
            if (section.CompositeFileElement.Providers.Count == 0)
            {
                //create new providers
                PageHeaderProvider php = new PageHeaderProvider();
                php.Initialize(PageHeaderProvider.DefaultName, null);
                m_FileRegisterProviders.Add(php);

                LazyLoadProvider csrp = new LazyLoadProvider();
                csrp.Initialize(LazyLoadProvider.DefaultName, null);
                m_FileRegisterProviders.Add(csrp);

                LoaderControlProvider lcp = new LoaderControlProvider();
                lcp.Initialize(LoaderControlProvider.DefaultName, null);
                m_FileRegisterProviders.Add(lcp);
            }
            else
            {
                ProvidersHelper.InstantiateProviders(section.FileRegistrationElement.Providers, m_FileRegisterProviders, typeof(BaseFileRegistrationProvider));
            }

        }

        private void LoadDefaultCompositeFileConfig(ClientDependencySection section)
        {
            if (section.CompositeFileElement.Providers.Count == 0)
            {
                CompositeFileProcessingProvider cfpp = new CompositeFileProcessingProvider();
                cfpp.Initialize(CompositeFileProcessingProvider.DefaultName, null);
                m_CompositeFileProviders.Add(cfpp);
            }
            else
            {
                ProvidersHelper.InstantiateProviders(section.CompositeFileElement.Providers, m_CompositeFileProviders, typeof(BaseCompositeFileProcessingProvider));
            }
            
        }

        private void LoadDefaultMvcFileConfig(ClientDependencySection section)
        {
            if (section.MvcElement.Renderers.Count == 0)
            {
                var mvc = new StandardRenderer();
                mvc.Initialize(StandardRenderer.DefaultName, null);
                m_MvcRenderers.Add(mvc);
            }
            else
            {
                ProvidersHelper.InstantiateProviders(section.MvcElement.Renderers, m_MvcRenderers, typeof(BaseRenderer));
            }

        }
    }
}

