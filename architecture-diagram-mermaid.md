Here's the updated Mermaid architecture diagrams with correct executable names and improved styling:

**Architecture Overview Diagram:**

```mermaid
graph TB
    subgraph "Client System (Windows)"
        CLI[Tracker.Cli.exe<br/>Command Line Interface]
        Service[Tracker.Service.exe<br/>Windows Background Service]
        Config[Configuration<br/>%ProgramData%\Tracker\config.json]
        Logs[Service Logs<br/>%ProgramData%\Tracker\logs\service-.log]
        DeviceInfo[Device Information Collector]
        
        subgraph "Device Information"
            SystemInfo[System Info<br/>Device ID, Hostname, OS]
            Network[Network Info<br/>IP, MAC, Wi-Fi]
            Hardware[Hardware Info<br/>CPU, RAM, Disk]
            Battery[Battery Info<br/>Status, Percentage]
        end
    end

    subgraph "Network Layer"
        HTTP[HTTPS/HTTP<br/>JSON Reports]
        Internet[Internet]
    end

    subgraph "Server System"
        Server[Tracker.Server.exe<br/>ASP.NET Core Web API]
        
        subgraph "Server Endpoints"
            HealthEndpoint[GET /api/health<br/>Health Check]
            ReportEndpoint[POST /api/report<br/>Receive Reports]
        end
        
        subgraph "Server Features"
            GeoService[GeoLocation Service<br/>ipapi.co / ip-api.com]
            Logger[Structured Logging<br/>Serilog]
            Parser[Report Parser<br/>JSON Deserialization]
        end
        
        ServerStorage[Server Logs<br/>logs/server-.log]
    end

    subgraph "Management"
        Admin[System Administrator]
    end

    %% Command flow
    Admin -->|"Tracker.Cli.exe commands"| CLI
    CLI -->|Install/Configure| Service
    CLI -->|Read/Write| Config
    Service -->|Read| Config
    Service -->|Write| Logs

    %% Data collection
    Service -->|Collect| DeviceInfo
    DeviceInfo -->|Get| SystemInfo
    DeviceInfo -->|Get| Network
    DeviceInfo -->|Get| Hardware
    DeviceInfo -->|Get| Battery

    %% Report flow
    Service -->|Send Reports| HTTP
    HTTP -->|via| Internet
    Internet -->|to| Server

    %% Server processing
    Server -->|Receive at| ReportEndpoint
    ReportEndpoint -->|Parse| Parser
    Parser -->|Geolocate| GeoService
    GeoService -->|"Get location for IP"| Internet
    Server -->|Log| Logger
    Logger -->|Write| ServerStorage

    %% Health checks
    Admin -->|Check| HealthEndpoint
    HealthEndpoint -->|Return status| Admin

    %% Styles
    classDef client fill:#e1f5fe,stroke:#01579b,stroke-width:2px,color:#000
    classDef server fill:#f3e5f5,stroke:#4a148c,stroke-width:2px,color:#000
    classDef management fill:#fff3e0,stroke:#e65100,stroke-width:2px,color:#000
    classDef network fill:#e8f5e9,stroke:#1b5e20,stroke-width:2px,color:#000
    classDef component fill:#fce4ec,stroke:#880e4f,stroke-width:1px,color:#000

    class CLI,Service,Config,Logs,DeviceInfo client
    class SystemInfo,Network,Hardware,Battery component
    class Server,HealthEndpoint,ReportEndpoint,GeoService,Logger,Parser,ServerStorage server
    class Admin management
    class HTTP,Internet network
```

**Component Architecture Diagram:**

```mermaid
flowchart TB
    subgraph "CLI Layer - Tracker.Cli.exe"
        CM[Command Manager]
        SC[Service Controller]
        CF[Config Manager]
        
        CM --> SC
        CM --> CF
    end

    subgraph "Service Layer - Tracker.Service.exe"
        WS[Windows Service Host]
        Worker[Worker Service]
        Timer[Report Timer]
        Retry[Retry Mechanism]
        
        WS --> Worker
        Worker --> Timer
        Worker --> Retry
    end

    subgraph "Collection Layer - Tracker.Shared.dll"
        Collector[DeviceInfoCollector]
        
        subgraph "Collectors"
            IP[IP Collector<br/>Public/Local]
            MAC[MAC Collector<br/>Network Interface]
            System[System Collector<br/>OS/Hostname]
            HW[Hardware Collector<br/>CPU/RAM/Disk]
            Battery[Battery Collector<br/>Status/Percentage]
            Wifi[WiFi Collector<br/>SSID/Network]
        end
        
        Collector --> IP
        Collector --> MAC
        Collector --> System
        Collector --> HW
        Collector --> Battery
        Collector --> Wifi
    end

    subgraph "Report Layer"
        Reporter[Report Sender]
        HTTPClient[HTTP Client]
        Serializer[JSON Serializer]
        
        Reporter --> Retry
        Retry --> HTTPClient
        Reporter --> Serializer
    end

    subgraph "Server Layer - Tracker.Server.exe"
        WebAPI[ASP.NET Core Web API]
        
        subgraph "Endpoints"
            Health[GET /api/health]
            Report[POST /api/report]
        end
        
        subgraph "Services"
            Geo[GeoLocation Service]
            LogService[Logging Service]
        end
        
        WebAPI --> Health
        WebAPI --> Report
        Report --> Geo
        Report --> LogService
    end

    subgraph "Storage Layer"
        FileSystem[File System]
        ConfigFile[%ProgramData%\Tracker\config.json]
        LogFiles[*.log]
        
        FileSystem --> ConfigFile
        FileSystem --> LogFiles
    end

    subgraph "External Services"
        GeoProviders[Geo Providers<br/>ipapi.co<br/>ip-api.com<br/>freeipapi.com]
    end

    %% Connections
    CM -->|Manage| WS
    CF -->|Read/Write| ConfigFile
    
    Worker -->|Schedule| Timer
    Timer -->|Trigger| Collector
    Collector -->|Return Data| Reporter
    Reporter -->|Send| HTTPClient
    HTTPClient -->|POST| Report
    
    Report -->|Lookup| Geo
    Geo -->|Query| GeoProviders
    
    Worker -->|Log| LogFiles
    Report -->|Log| LogFiles
    CF -->|Read| ConfigFile

    %% Styling
    classDef cli fill:#bbdefb,stroke:#1565c0,stroke-width:2px,color:#000
    classDef service fill:#c8e6c9,stroke:#2e7d32,stroke-width:2px,color:#000
    classDef collector fill:#ffccbc,stroke:#bf360c,stroke-width:2px,color:#000
    classDef report fill:#d1c4e9,stroke:#4a148c,stroke-width:2px,color:#000
    classDef server fill:#b3e5fc,stroke:#01579b,stroke-width:2px,color:#000
    classDef storage fill:#fff9c4,stroke:#f57f17,stroke-width:2px,color:#000
    classDef external fill:#f8bbd0,stroke:#880e4f,stroke-width:2px,color:#000

    class CM,SC,CF cli
    class WS,Worker,Timer,Retry service
    class Collector,IP,MAC,System,HW,Battery,Wifi collector
    class Reporter,HTTPClient,Serializer report
    class WebAPI,Health,Report,Geo,LogService server
    class FileSystem,ConfigFile,LogFiles storage
    class GeoProviders external
```

**Sequence Diagram:**

```mermaid
sequenceDiagram
    participant Admin
    participant CLI as Tracker.Cli.exe
    participant Service as Tracker.Service.exe
    participant Collector as DeviceInfoCollector
    participant Config as Configuration
    participant Server as Tracker.Server.exe
    participant Geo as GeoLocation Service
    participant Logs as Logging System

    %% Installation
    Admin->>CLI: Tracker.Cli.exe install
    CLI->>Service: Install Windows Service
    Service-->>CLI: Installation Complete
    CLI-->>Admin: ✅ Service installed

    Admin->>CLI: Tracker.Cli.exe start
    CLI->>Service: Start Service
    Service-->>CLI: Service Started
    CLI-->>Admin: ✅ Service running

    %% Configuration
    Admin->>CLI: Tracker.Cli.exe set webhook <url>
    CLI->>Config: Save webhook URL
    Config-->>CLI: Saved
    CLI-->>Admin: ✅ Webhook configured

    Admin->>CLI: Tracker.Cli.exe set interval 180
    CLI->>Config: Save interval
    Config-->>CLI: Saved
    CLI-->>Admin: ✅ Interval set

    %% Normal Operation - Periodic Reports
    loop Every X seconds (configurable)
        Service->>Collector: Collect device info
        Collector->>Collector: Get System Info
        Collector->>Collector: Get Network Info
        Collector->>Collector: Get Hardware Info
        Collector->>Collector: Get Battery Info
        Collector-->>Service: DeviceInfo object
        
        Service->>Config: Load config
        Config-->>Service: Webhook URL
        
        Service->>Server: POST /api/report (JSON)
        Server->>Geo: GetLocation(IP)
        Geo->>Geo: Query ipapi.co
        Geo-->>Server: Location data
        Server->>Server: Process & enrich report
        Server->>Logs: Log received data
        Server-->>Service: 200 OK
        Service->>Logs: Log success
    end

    %% Retry on Failure
    Service->>Server: POST /api/report (JSON)
    Server-->>Service: 500 Error
    Service->>Service: Wait 5 seconds
    Service->>Server: POST /api/report (JSON) - Retry 1
    Server-->>Service: 200 OK
    Service->>Logs: Log retry success

    %% Test Mode
    Admin->>CLI: Tracker.Cli.exe test
    CLI->>Config: Load config
    Config-->>CLI: Webhook URL
    CLI->>Server: POST test data
    Server-->>CLI: Response
    CLI-->>Admin: ✅ Test complete

    %% Run Once
    Admin->>CLI: Tracker.Cli.exe run-once
    CLI->>Collector: Collect device info
    Collector-->>CLI: DeviceInfo
    CLI->>Server: POST /api/report
    Server-->>CLI: Response
    CLI-->>Admin: ✅ Report sent

    %% Config View
    Admin->>CLI: Tracker.Cli.exe config
    CLI->>Config: Load config
    Config-->>CLI: Configuration data
    CLI-->>Admin: Display configuration

    %% Status Check
    Admin->>CLI: Tracker.Cli.exe status
    CLI->>Service: Check status
    Service-->>CLI: Running/Stopped
    CLI-->>Admin: Service status

    %% Shutdown
    Admin->>CLI: Tracker.Cli.exe stop
    CLI->>Service: Stop Service
    Service->>Logs: Log shutdown
    Service-->>CLI: Service Stopped
    CLI-->>Admin: ✅ Service stopped
```
