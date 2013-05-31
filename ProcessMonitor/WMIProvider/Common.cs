namespace WMIProvider
{
    /// <summary>
    /// Defines the return status of an action invoked on a Windows Service or a Process
    /// </summary>
    public enum ProcessReturnCode
    {
        /// <summary>
        /// Success
        /// </summary>
        Success = 0,
        /// <summary>
        /// NotSupported
        /// </summary>
        NotSupported = 1,
        /// <summary>
        /// AccessDenied
        /// </summary>
        AccessDenied = 2,
        /// <summary>
        /// DependentServicesRunning
        /// </summary>
        DependentServicesRunning = 3,
        /// <summary>
        /// DependentServicesRunning
        /// </summary>
        InvalidServiceControl = 4,
        /// <summary>
        /// ServiceCannotAcceptControl
        /// </summary>
        ServiceCannotAcceptControl = 5,
        /// <summary>
        /// ServiceNotActive
        /// </summary>
        ServiceNotActive = 6,
        /// <summary>
        /// ServiceRequestTimeout
        /// </summary>
        ServiceRequestTimeout = 7,
        /// <summary>
        /// UnknownFailure
        /// </summary>
        UnknownFailure = 8,
        /// <summary>
        /// PathNotFound
        /// </summary>
        PathNotFound = 9,
        /// <summary>
        /// ServiceAlreadyRunning
        /// </summary>
        ServiceAlreadyRunning = 10,
        /// <summary>
        /// ServiceDatabaseLocked
        /// </summary>
        ServiceDatabaseLocked = 11,
        /// <summary>
        /// ServiceDependencyDeleted
        /// </summary>
        ServiceDependencyDeleted = 12,
        /// <summary>
        /// ServiceDependencyFailure
        /// </summary>
        ServiceDependencyFailure = 13,
        /// <summary>
        /// ServiceDisabled
        /// </summary>
        ServiceDisabled = 14,
        /// <summary>
        /// ServiceLogonFailure
        /// </summary>
        ServiceLogonFailure = 15,
        /// <summary>
        /// ServiceMarkedForDeletion
        /// </summary>
        ServiceMarkedForDeletion = 16,
        /// <summary>
        /// ServiceNoThread
        /// </summary>
        ServiceNoThread = 17,
        /// <summary>
        /// StatusCircularDependency
        /// </summary>
        StatusCircularDependency = 18,
        /// <summary>
        /// StatusDuplicateName
        /// </summary>
        StatusDuplicateName = 19,
        /// <summary>
        /// StatusInvalidName
        /// </summary>
        StatusInvalidName = 20,
        /// <summary>
        /// StatusInvalidParameter
        /// </summary>
        StatusInvalidParameter = 21,
        /// <summary>
        /// StatusInvalidServiceAccount
        /// </summary>
        StatusInvalidServiceAccount = 22,
        /// <summary>
        /// StatusServiceExists
        /// </summary>
        StatusServiceExists = 23,
        /// <summary>
        /// ServiceAlreadyPaused
        /// </summary>
        ServiceAlreadyPaused = 24,
        /// <summary>
        /// ServiceNotFound
        /// </summary>
        ServiceNotFound = 25
    }

}
