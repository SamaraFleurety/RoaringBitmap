using System;
using System.Collections.Generic;
using System.IO;

namespace Collections.Special
{
    internal class ArrayContainer : Container, IEquatable<ArrayContainer>
    {
        public static readonly ArrayContainer One;
        private int m_Cardinality; //就等于一般写的count
        internal ushort[] m_Content;
        private int m_Capacity;

        static ArrayContainer()
        {
            var data = new ushort[MaxSize];
            for (ushort i = 0; i < MaxSize; i++)
            {
                data[i] = i;
            }
            One = new ArrayContainer(MaxSize, data);
        }

        internal ArrayContainer()
        {
            m_Content = new ushort[64];
            m_Cardinality = 0;
            m_Capacity = 64;
        }
        private ArrayContainer(int cardinality, ushort[] data)
        {
            m_Content = data;
            m_Cardinality = cardinality;
            m_Capacity = data.Length;
        }

        protected internal override int Cardinality => m_Cardinality;

        public override int ArraySizeInBytes => m_Cardinality * sizeof(ushort);


        public bool Equals(ArrayContainer other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (m_Cardinality != other.m_Cardinality)
            {
                return false;
            }
            for (var i = 0; i < m_Cardinality; i++)
            {
                if (m_Content[i] != other.m_Content[i])
                {
                    return false;
                }
            }
            return true;
        }

        internal static ArrayContainer Create(ushort[] values)
        {
            return new ArrayContainer(values.Length, values);
        }

        internal static ArrayContainer Create(BitmapContainer bc)
        {
            var data = new ushort[bc.Cardinality];
            var cardinality = bc.FillArray(data);
            var result = new ArrayContainer(cardinality, data);
            return result;
        }

        protected override bool EqualsInternal(Container other)
        {
            var ac = other as ArrayContainer;
            return (ac != null) && Equals(ac);
        }

        public override IEnumerator<ushort> GetEnumerator()
        {
            for (var i = 0; i < m_Cardinality; i++)
            {
                yield return m_Content[i];
            }
        }

        #region 位运算
        public static Container operator &(ArrayContainer x, ArrayContainer y)
        {
            var desiredCapacity = Math.Min(x.m_Cardinality, y.m_Cardinality);
            var data = new ushort[desiredCapacity];
            var calculatedCardinality = Util.IntersectArrays(x.m_Content, x.m_Cardinality, y.m_Content, y.m_Cardinality, data);
            return new ArrayContainer(calculatedCardinality, data);
        }

        public static ArrayContainer operator &(ArrayContainer x, BitmapContainer y)
        {
            var data = new ushort[x.m_Content.Length];
            var c = x.m_Cardinality;
            var pos = 0;
            for (var i = 0; i < c; i++)
            {
                var v = x.m_Content[i];
                if (y.Contains(v))
                {
                    data[pos++] = v;
                }
            }
            return new ArrayContainer(pos, data);
        }

        public static Container operator |(ArrayContainer x, ArrayContainer y)
        {
            var totalCardinality = x.m_Cardinality + y.m_Cardinality;
            if (totalCardinality > MaxSize)
            {
                var output = new ushort[totalCardinality];
                var calcCardinality = Util.UnionArrays(x.m_Content, x.m_Cardinality, y.m_Content, y.m_Cardinality, output);
                if (calcCardinality > MaxSize)
                {
                    return BitmapContainer.Create(calcCardinality, output);
                }
                return new ArrayContainer(calcCardinality, output);
            }
            var desiredCapacity = totalCardinality;
            var data = new ushort[desiredCapacity];
            var calculatedCardinality = Util.UnionArrays(x.m_Content, x.m_Cardinality, y.m_Content, y.m_Cardinality, data);
            return new ArrayContainer(calculatedCardinality, data);
        }

        public static Container operator |(ArrayContainer x, BitmapContainer y)
        {
            return y | x;
        }

        public static Container operator ~(ArrayContainer x)
        {
            return BitmapContainer.Create(x.m_Cardinality, x.m_Content, true); // an arraycontainer only contains up to 4096 values, so the negation is a bitmap container
        }

        public static Container operator ^(ArrayContainer x, ArrayContainer y)
        {
            var totalCardinality = x.m_Cardinality + y.m_Cardinality;
            if (totalCardinality > MaxSize)
            {
                var bc = BitmapContainer.CreateXor(x.m_Content, x.Cardinality, y.m_Content, y.Cardinality);
                if (bc.Cardinality <= MaxSize)
                {
                    Create(bc);
                }
            }
            var desiredCapacity = totalCardinality;
            var data = new ushort[desiredCapacity];
            var calculatedCardinality = Util.XorArrays(x.m_Content, x.m_Cardinality, y.m_Content, y.m_Cardinality, data);
            return new ArrayContainer(calculatedCardinality, data);
        }

        public static Container operator ^(ArrayContainer x, BitmapContainer y)
        {
            return y ^ x;
        }

        public static Container AndNot(ArrayContainer x, ArrayContainer y)
        {
            var desiredCapacity = x.m_Cardinality;
            var data = new ushort[desiredCapacity];
            var calculatedCardinality = Util.DifferenceArrays(x.m_Content, x.m_Cardinality, y.m_Content, y.m_Cardinality, data);
            return new ArrayContainer(calculatedCardinality, data);
        }

        public static Container AndNot(ArrayContainer x, BitmapContainer y)
        {
            var data = new ushort[x.m_Content.Length];
            var c = x.m_Cardinality;
            var pos = 0;
            for (var i = 0; i < c; i++)
            {
                var v = x.m_Content[i];
                if (!y.Contains(v))
                {
                    data[pos++] = v;
                }
            }
            return new ArrayContainer(pos, data);
        }
        public int OrArray(ulong[] bitmap)
        {
            var extraCardinality = 0;
            var yC = m_Cardinality;
            for (var i = 0; i < yC; i++)
            {
                var yValue = m_Content[i];
                var index = yValue >> 6;
                var previous = bitmap[index];
                var after = previous | (1UL << yValue);
                bitmap[index] = after;
                extraCardinality += (int) ((previous - after) >> 63);
            }
            return extraCardinality;
        }

        public int XorArray(ulong[] bitmap)
        {
            var extraCardinality = 0;
            var yC = m_Cardinality;
            for (var i = 0; i < yC; i++)
            {
                var yValue = m_Content[i];
                var index = yValue >> 6;
                var previous = bitmap[index];
                var mask = 1UL << yValue;
                bitmap[index] = previous ^ mask;
                extraCardinality += (int) (1 - 2 * ((previous & mask) >> yValue));
            }
            return extraCardinality;
        }


        public int AndNotArray(ulong[] bitmap)
        {
            var extraCardinality = 0;
            var yC = m_Cardinality;
            for (var i = 0; i < yC; i++)
            {
                var yValue = m_Content[i];
                var index = yValue >> 6;
                var previous = bitmap[index];
                var after = previous & ~(1UL << yValue);
                bitmap[index] = after;
                extraCardinality -= (int) ((previous ^ after) >> yValue);
            }
            return extraCardinality;
        }

        #endregion
        public override bool Equals(object obj)
        {
            var ac = obj as ArrayContainer;
            return (ac != null) && Equals(ac);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var code = 17;
                code = code * 23 + m_Cardinality;
                for (var i = 0; i < m_Cardinality; i++)
                {
                    code = code * 23 + m_Content[i];
                }
                return code;
            }
        }

        #region 序列化
        public static void Serialize(ArrayContainer ac, BinaryWriter binaryWriter)
        {
            for (var i = 0; i < ac.m_Cardinality; i++)
            {
                binaryWriter.Write(ac.m_Content[i]);
            }
        }

        public static ArrayContainer Deserialize(BinaryReader binaryReader, int cardinality)
        {
            var data = new ushort[cardinality];
            for (var i = 0; i < cardinality; i++)
            {
                data[i] = binaryReader.ReadUInt16();
            }
            return new ArrayContainer(cardinality, data);
        }
        #endregion

        #region 动态元素
        private void EnsureCapacity(int min)
        {
            if (min > Container.MaxSize)
            {
                // 如果需要的容量超过 4096，抛出异常或触发转换
                // 实际上应该在 Add 方法中检查并转换容器类型
                throw new InvalidOperationException(
                    $"ArrayContainer capacity cannot exceed {Container.MaxSize}. " +
                    $"Consider converting to BitmapContainer.");
            }

            if (m_Capacity == Container.MaxSize) return;

            if (m_Capacity < min)
            {
                int newCapacity = Math.Max(m_Capacity * 2, min);
                Array.Resize(ref m_Content, newCapacity);
                m_Capacity = newCapacity;
            }
        }
        public override bool Add(ushort value)
        {
            if (m_Cardinality >= Container.MaxSize)
            {
                // 不应该由 ArrayContainer 处理，应该转换为 BitmapContainer
                // 这个检查应该在 RoaringArray.CheckAndConvertContainer 中处理
                return false;
            }

            // 二分查找
            int index = BinarySearch(value);

            if (index >= 0)
            {
                return false;  // 已存在
            }

            // 插入到新位置
            index = ~index;
            EnsureCapacity(m_Cardinality + 1);

            // 移动元素
            if (index < m_Cardinality)
            {
                Array.Copy(m_Content, index, m_Content, index + 1, m_Cardinality - index);
            }

            m_Content[index] = value;
            m_Cardinality++;

            return true;
        }

        public override bool Remove(ushort value)
        {
            int index = BinarySearch(value);

            if (index < 0)
            {
                return false;  // 不存在
            }

            // 移除元素
            if (index < m_Cardinality - 1)
            {
                Array.Copy(m_Content, index + 1, m_Content, index, m_Cardinality - index - 1);
            }

            m_Cardinality--;
            return true;
        }

        public override bool Contains(ushort value)
        {
            return BinarySearch(value) >= 0;
        }

        // 二分查找辅助方法，返回找到的下标
        private int BinarySearch(ushort value)
        {
            int left = 0;
            int right = m_Cardinality - 1;

            while (left <= right)
            {
                int mid = (left + right) >> 1;

                if (m_Content[mid] == value)
                {
                    return mid;
                }
                else if (m_Content[mid] < value)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            //这是返回的可插入位置
            return ~left;
        }
        #endregion
    }
}