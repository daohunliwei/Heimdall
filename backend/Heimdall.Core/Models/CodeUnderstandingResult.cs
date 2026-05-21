namespace Heimdall.Core.Models;

/// <summary>
/// 深度代码理解的完整输出结果——供结构规划和页面生成阶段消费。
/// </summary>
public class CodeUnderstandingResult
{
    /// <summary>方法级调用图。</summary>
    public CallGraph CallGraph { get; set; } = new();

    /// <summary>模块依赖拓扑。</summary>
    public DependencyTopology DependencyTopology { get; set; } = new();

    /// <summary>检测到的设计模式列表。</summary>
    public List<DetectedPattern> DesignPatterns { get; set; } = new();

    /// <summary>LLM 辅助的架构理解洞察。</summary>
    public ArchitectureInsight ArchitectureInsight { get; set; } = new();
}

/// <summary>
/// 方法级调用图。
/// </summary>
public class CallGraph
{
    /// <summary>调用关系边列表。</summary>
    public List<CallEdge> Edges { get; set; } = new();

    /// <summary>调用图中节点（方法/函数）总数。</summary>
    public int NodeCount { get; set; }

    /// <summary>最大调用深度。</summary>
    public int MaxDepth { get; set; }
}

/// <summary>
/// 调用图中的一条边——表示一个方法调用另一个方法。
/// </summary>
public class CallEdge
{
    /// <summary>调用者符号名（如 UserService.GetById）。</summary>
    public string CallerSymbol { get; set; } = string.Empty;

    /// <summary>调用者所在文件路径。</summary>
    public string CallerFilePath { get; set; } = string.Empty;

    /// <summary>被调用者符号名（如 IUserRepository.FindAsync）。</summary>
    public string CalleeSymbol { get; set; } = string.Empty;

    /// <summary>被调用者所在文件路径（跨文件调用时有值）。</summary>
    public string? CalleeFilePath { get; set; }

    /// <summary>调用类型：Direct/Interface/Event/Delegate。</summary>
    public string CallType { get; set; } = "Direct";

    /// <summary>置信度评分（0-1），低置信度可能为误报。</summary>
    public double Confidence { get; set; } = 0.8;
}

/// <summary>
/// 模块依赖拓扑图。
/// </summary>
public class DependencyTopology
{
    /// <summary>模块列表。</summary>
    public List<ModuleNode> Modules { get; set; } = new();

    /// <summary>依赖边列表。</summary>
    public List<DependencyEdge> Edges { get; set; } = new();

    /// <summary>循环依赖路径列表（每个元素为参与循环的模块名序列）。</summary>
    public List<List<string>> CyclicPaths { get; set; } = new();
}

/// <summary>
/// 拓扑图中的模块节点。
/// </summary>
public class ModuleNode
{
    /// <summary>模块名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>模块类型（project/package/namespace/directory）。</summary>
    public string ModuleType { get; set; } = "directory";

    /// <summary>包含的文件数。</summary>
    public int FileCount { get; set; }

    /// <summary>入口点文件列表。</summary>
    public List<string> EntryPoints { get; set; } = new();
}

/// <summary>
/// 模块间依赖边。
/// </summary>
public class DependencyEdge
{
    /// <summary>源模块名称。</summary>
    public string FromModule { get; set; } = string.Empty;

    /// <summary>目标模块名称。</summary>
    public string ToModule { get; set; } = string.Empty;

    /// <summary>依赖类型：Compile/Runtime/Test。</summary>
    public string DependencyType { get; set; } = "Compile";
}

/// <summary>
/// 检测到的设计模式。
/// </summary>
public class DetectedPattern
{
    /// <summary>模式名称（Factory/Strategy/Observer/Builder/Singleton 等）。</summary>
    public string PatternName { get; set; } = string.Empty;

    /// <summary>参与该模式的类/接口列表。</summary>
    public List<PatternParticipant> Participants { get; set; } = new();

    /// <summary>检测置信度（0-1）。</summary>
    public double Confidence { get; set; } = 0.7;

    /// <summary>所在模块名称。</summary>
    public string? ModuleName { get; set; }
}

/// <summary>
/// 设计模式中的参与者。
/// </summary>
public class PatternParticipant
{
    /// <summary>类/接口名称。</summary>
    public string SymbolName { get; set; } = string.Empty;

    /// <summary>在模式中的角色（如 Factory 模式中的 Creator/Product）。</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>所在文件路径。</summary>
    public string FilePath { get; set; } = string.Empty;
}

/// <summary>
/// LLM 辅助的架构理解洞察。
/// </summary>
public class ArchitectureInsight
{
    /// <summary>识别到的架构模式（Layered/Microservices/CQRS/EventDriven 等）。</summary>
    public string ArchitecturePattern { get; set; } = string.Empty;

    /// <summary>架构模式的详细描述。</summary>
    public string PatternDescription { get; set; } = string.Empty;

    /// <summary>核心数据流路径描述列表。</summary>
    public List<DataFlowPath> DataFlows { get; set; } = new();

    /// <summary>关键设计决策推断。</summary>
    public List<string> DesignDecisions { get; set; } = new();

    /// <summary>各层/服务职责描述。</summary>
    public List<LayerDescription> Layers { get; set; } = new();
}

/// <summary>
/// 数据流路径。
/// </summary>
public class DataFlowPath
{
    /// <summary>流路径名称（如 "用户请求处理流程"）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>流经的组件列表。</summary>
    public List<string> Components { get; set; } = new();

    /// <summary>描述。</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 架构层描述。
/// </summary>
public class LayerDescription
{
    /// <summary>层名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>职责描述。</summary>
    public string Responsibility { get; set; } = string.Empty;

    /// <summary>关键模块列表。</summary>
    public List<string> KeyModules { get; set; } = new();
}
