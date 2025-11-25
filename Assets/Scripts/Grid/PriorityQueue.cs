using System;

public class PriorityQueue<T> where T : IComparable<T>
{
    class Node
    {
        public T data { get; private set; }
        public int priority;
        private static bool compareDataForPriority;
        public Node(T nData, int priority, bool shouldCompareDataForPriority)
        {
            data = nData;
            this.priority = priority;
            compareDataForPriority = shouldCompareDataForPriority;
        }

        public static bool operator <(Node n1, Node n2)
        {
            return compareDataForPriority ? n1.data.CompareTo(n2.data) < 0 : (n1.priority < n2.priority);
        }
        public static bool operator >(Node n1, Node n2)
        {
            return compareDataForPriority ? n1.data.CompareTo(n2.data) > 0 : (n1.priority > n2.priority);
        }
        public static bool operator <=(Node n1, Node n2)
        {
            return compareDataForPriority ? n1.data.CompareTo(n2.data) <= 0 : (n1.priority <= n2.priority);
        }
        public static bool operator >=(Node n1, Node n2)
        {
            return compareDataForPriority ? n1.data.CompareTo(n2.data) >= 0 : (n1.priority >= n2.priority);
        }
        public static bool operator ==(Node n1, Node n2)
        {
            return (compareDataForPriority ? n1.data.CompareTo(n2.data) == 0 : n1.priority == n2.priority) && (dynamic)n1.data == (dynamic)n2.data;
        }
        public static bool operator !=(Node n1, Node n2)
        {
            return (compareDataForPriority ? n1.data.CompareTo(n2.data) != 0 : n1.priority == n2.priority) && (dynamic)n1.data != (dynamic)n2.data;
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    #region Variables and Properties
    Node[] arr;
    private int count;
    private const int arrSize = 100;
    private bool compareDataForPriority;
    #endregion

    #region Methods
    public PriorityQueue(bool compareDataForPriority)
    {
        arr = new Node[arrSize];
        count = 0;
        this.compareDataForPriority = compareDataForPriority;
    }

    public void Enqueue(T nData)
    {
        if (count == arr.Length)
        {
            throw new Exception("Priority Queue is at full capacity");
        }
        else
        {
            arr[count] = new Node(nData, 0, compareDataForPriority);
            count++;
            siftUp(count - 1);
        }
    }
    public void Enqueue(T nData, int priority)
    {
        if (count == arr.Length)
        {
            throw new Exception("Priority Queue is at full capacity");
        }
        else
        {
            arr[count] = new Node(nData, priority, compareDataForPriority);
            count++;
            siftUp(count - 1);
        }
    }

    private void siftUp(int index)
    {
        int parentIndex;
        Node temp;
        if (index != 0)
        {
            parentIndex = getParentIndex(index);
            if (arr[parentIndex] > arr[index])
            {
                temp = arr[parentIndex];
                arr[parentIndex] = arr[index];
                arr[index] = temp;
                siftUp(parentIndex);
            }
        }
    }

    private int getParentIndex(int index)
    {
        return (index - 1) / 2;
    }

    public void decrease_priority(T target, int newPriority)
    {
        //find the target first
        for (int i = 0; i < arr.Length; i++)
        {
            if ((dynamic)arr[i].data == (dynamic)target)
            {
                if (newPriority > arr[i].priority)
                {
                    throw new Exception("New priority assignment must be less than current priority");
                }
                else
                {
                    arr[i].priority = newPriority;
                    siftUp(i);
                }
                break;
            }
        }
    }

    public T dequeue_min()
    {
        Node nObj;
        if (count == 0)
        {
            throw new Exception("Priority Queue is empty");
        }
        else
        {
            nObj = arr[0];
            arr[0] = arr[count - 1];
            count--;
            if (count > 0)
            {
                siftDown(0);
            }
        }
        arr[count] = null;//set the last index to null after a dequeue min
        return nObj.data;
    }

    public void siftDown(int nodeIndex)
    {
        int leftChildIndex, rightChildIndex, minIndex;
        Node temp;
        leftChildIndex = getLeftChildIndex(nodeIndex);
        rightChildIndex = getRightChildIndex(nodeIndex);
        if (rightChildIndex >= count)
        {
            if (leftChildIndex >= count)
            {
                return;
            }
            else
            {
                minIndex = leftChildIndex;
            }
        }
        else
        {
            if (arr[leftChildIndex] <= arr[rightChildIndex]) { minIndex = leftChildIndex; } else { minIndex = rightChildIndex; }
        }
        if (arr[nodeIndex] > arr[minIndex])
        {
            temp = arr[minIndex];
            arr[minIndex] = arr[nodeIndex];
            arr[nodeIndex] = temp;
            siftDown(minIndex);
        }
    }

    private int getLeftChildIndex(int index)
    {
        return (2 * index) + 1;
    }

    private int getRightChildIndex(int index)
    {
        return (2 * index) + 2;
    }

    public T peek()
    {
        return arr[0].data;
    }

    public bool empty()
    {
        return (count == 0);
    }
    #endregion
}