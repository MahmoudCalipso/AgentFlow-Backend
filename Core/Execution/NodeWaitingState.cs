using System.Collections.Generic;
using System.Linq;

namespace AgentFlow.Backend.Core.Execution;

public sealed class NodeWaitingState {
    private readonly int _requiredInputs;
    private readonly List<IReadOnlyList<ExecutionItem>?> _inputs;

    public NodeWaitingState(int requiredInputs) {
        _requiredInputs = requiredInputs;
        _inputs = Enumerable.Repeat<IReadOnlyList<ExecutionItem>?>(null, requiredInputs).ToList();
    }

    public void AddInput(int index, IReadOnlyList<ExecutionItem> items) {
        lock (_inputs) {
            _inputs[index] = items;
        }
    }

    public bool IsReady => _inputs.All(i => i != null);

    public IReadOnlyList<ExecutionItem> GetMergedItems() {
        return _inputs.SelectMany(i => i!).ToList();
    }
}
