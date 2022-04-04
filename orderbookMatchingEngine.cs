using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Solution
{
    class Solution
    {
        static void Main(string[] args)
        {
            var ob = new Ob();
            var cmdFactory = new InputCommandFactory();

            while (true)
            {
                var inputLine = Console.ReadLine();
                var cmd = cmdFactory.Create(inputLine);

                if (cmd is SubInputCommand subCmd)
                {
                    Console.WriteLine(ob.SubmitOrder(subCmd));
                }
                else if (cmd is CxlInputCommand xclCmd)
                {
                    ob.ClearOrder(xclCmd.Id);
                }
                else if (cmd is CrpInputCommand crpCmd)
                {
                    ob.CancelReplaceOrder(crpCmd);
                }
                else if (cmd is EndInputCommand endCmd)
                {
                    ob.PrintBookStatus();
                    break;
                }

            }
        }
    }

    enum Side
    {
        B,
        S
    }

    class Ob
    {
        LinkedList<Order> B { get; set; } = new LinkedList<Order>();
        LinkedList<Order> S { get; set; } = new LinkedList<Order>();
        Dictionary<string, (Order order, Side side)> orderDictionary { get; set; } = new Dictionary<string, (Order, Side)>();


        public void PrintBookStatus()
        {
            Console.WriteLine("B: " + String.Join(" ", B.Select(x => x.ToString())));
            Console.WriteLine("S: " + String.Join(" ", S.Select(x => x.ToString())));
        }

        public void ClearOrder(string id)
        {
            if (orderDictionary.TryGetValue(id, out var orderStruct))
            {

                var book = orderStruct.side == Side.S ? S : B;
                RemoveOrder(orderStruct.order, book);
            }
        }

        private void RemoveOrder(Order o, LinkedList<Order> book)
        {
            
            book.Remove(o);
            orderDictionary.Remove(o.Id);
        }

        private bool OrderNotMatched(SubLoInputCommand cmd, Order order) =>
             (cmd.Side == Side.B && order.Price > cmd.Price) || (cmd.Side == Side.S && order.Price < cmd.Price);

        public int SubmitOrder(SubInputCommand cmd)
        {
            int totalCost = 0;
            (var book, var otherBook) = cmd.Side == Side.S ? (S, B) : (B, S);
            if (cmd is SubMoInputCommand subMoCmd)
            {
                while (otherBook.Any() && subMoCmd.Quantity > 0)
                {
                    var order = otherBook.First();

                    totalCost += ReduceOrders(order, subMoCmd);
                    RemoveOrderIfEmpty(otherBook, order);
                    RemoveReplenishReAddIcberg(cmd, otherBook, order);
                }
            }
            else if (cmd is SubLoInputCommand subLoCmd)
            {
                if (!(cmd is SubFOKCInputCommand) || CanFillFOKOrder(subLoCmd, otherBook))
                {
                    while (otherBook.Any() && subLoCmd.Quantity > 0)
                    {
                        var order = otherBook.First();
                        if (OrderNotMatched(subLoCmd, order))
                        {
                            break;
                        }

                        totalCost += ReduceOrders(order, subLoCmd);
                        RemoveOrderIfEmpty(otherBook, order);
                        RemoveReplenishReAddIcberg(cmd, otherBook, order);

                        if (subLoCmd is SubIcebergInputCommand ice)
                        {
                            ice.Replenish();
                        }
                    }

                    if (!(subLoCmd is SubIOCInputCommand))
                    {
                        InsertAsOrderInBookIfNotEmpty(subLoCmd, book);
                    }
                }

            }
            return totalCost;
        }

        private bool CanFillFOKOrder(SubLoInputCommand cmd, LinkedList<Order> book)
        {

            var bk = book.AsEnumerable();
            if (cmd.Side == Side.B)
            {
                bk = bk.TakeWhile(x => x.Price <= cmd.Price);
            }
            else
            {
                bk = bk.TakeWhile(x => x.Price >= cmd.Price);
            }

            var available = bk.Sum(x => x.Quantity + ((x as IcebergOrder)?.HiddenQuantity ?? 0));
            return cmd.Quantity <= available;
        }

        private void RemoveReplenishReAddIcberg(SubInputCommand cmd, LinkedList<Order> otherBook, Order order)
        {
            if (order.Quantity == 0 && order is IcebergOrder iceOrder)
            {
                var otherSide = cmd.Side == Side.S ? Side.B : Side.S;
                iceOrder.Replenish();
                InsertOrderInBookIfNotEmpty(order, otherSide, otherBook);
            }
        }

        private void InsertOrderInBookIfNotEmpty(Order order, Side side, LinkedList<Order> book)
        {
            if (order.Quantity > 0)
            {
                var cur = book.First;

                while (cur != null &&
                        (side == Side.B && cur.Value.Price >= order.Price ||
                         side == Side.S && cur.Value.Price <= order.Price)
                    )
                {
                    cur = cur.Next;
                }

                if (cur != null)
                {
                    book.AddBefore(cur, order);
                }
                else
                {
                    book.AddLast(order);
                }
                orderDictionary.Add(order.Id, (order, side));
            }
        }

        private void InsertAsOrderInBookIfNotEmpty(SubLoInputCommand subCmd, LinkedList<Order> book)
        {
            Order order = CreateOrder(subCmd);
            InsertOrderInBookIfNotEmpty(order, subCmd.Side, book);
        }

        private static Order CreateOrder(SubLoInputCommand subCmd)
        {
            Order order;
            if (subCmd is SubIcebergInputCommand ice)
            {
                order = new IcebergOrder(subCmd.Price, subCmd.Quantity, subCmd.Id, ice.HiddenQuantity + ice.Quantity);
            }
            else
            {
                order = new Order(subCmd.Price, subCmd.Quantity, subCmd.Id);
            }

            return order;
        }

        private void RemoveOrderIfEmpty(LinkedList<Order> otherBook, Order order)
        {
            if (order.Quantity == 0)
            {
                RemoveOrder(order, otherBook);
            }
        }

        int ReduceOrders(Order order, SubInputCommand subCmd)
        {
            var quantity = order.Quantity < subCmd.Quantity ? order.Quantity : subCmd.Quantity;
            order.Quantity -= quantity;
            subCmd.Quantity -= quantity;
            return order.Price * quantity;
        }

        internal void CancelReplaceOrder(CrpInputCommand crpCmd)
        {
            if (orderDictionary.TryGetValue(crpCmd.Id, out var o))
            {
                if(o.order is IcebergOrder)
                {
                    return;
                }

                var willMove = (o.order.Price != crpCmd.Price || o.order.Quantity <= crpCmd.Quantity);

                (o.order.Price, o.order.Quantity) = (crpCmd.Price, crpCmd.Quantity);

                if (willMove)
                {
                    var book = o.side == Side.B ? B : S;
                    RemoveOrder(o.order, book);
                    InsertOrderInBookIfNotEmpty(o.order, o.side, book);
                }

            }

        }
    }

    class InputCommandFactory
    {
        private SubInputCommandFactory subInputCommandFactory = new SubInputCommandFactory();
        public InputCommand Create(string input)
        {
            //var tokens = input.Split(' ', StringSplitOptions.None);
            var tokens = input.Split(' ');

            switch (tokens[0])
            {
                case "SUB": return subInputCommandFactory.Create(tokens);
                case "CXL": return new CxlInputCommand(tokens);
                case "CRP": return new CrpInputCommand(tokens);
                case "END": return new EndInputCommand(tokens);
                case "": throw new ArgumentException("Empty arg");
            }
            throw new ArgumentException("Invalid arg");
        }
    }

    class SubInputCommandFactory
    {

        public InputCommand Create(string[] tokens)
        {
            switch (tokens[1])
            {
                case "MO": return new SubMoInputCommand(tokens);
                case "LO": return new SubLoInputCommand(tokens);
                case "IOC": return new SubIOCInputCommand(tokens);
                case "FOK": return new SubFOKCInputCommand(tokens);
                case "ICE": return new SubIcebergInputCommand(tokens);
                case "": throw new ArgumentException("Empty arg");
            }
            throw new ArgumentException("Invalid arg");
        }
    }


    abstract class InputCommand
    {
        public InputCommand(string[] tokens)
        {
            Command = tokens[0];
        }
        public string Command { get; set; }
    }

    class SubInputCommand : InputCommand
    {
        public SubInputCommand(string[] tokens) : base(tokens)
        {
            (Type, Side, Id, Quantity) = (tokens[1], tokens[2] == "B" ? Side.B : Side.S, tokens[3], Convert.ToInt32(tokens[4]));
        }

        public string Type { get; set; }
        public Side Side { get; set; }
        public string Id { get; set; }
        public int Quantity { get; set; }
    }



    class SubMoInputCommand : SubInputCommand
    {

        public SubMoInputCommand(string[] tokens) : base(tokens)
        { }
    }

    class SubLoInputCommand : SubInputCommand
    {

        public SubLoInputCommand(string[] tokens) : base(tokens)
        {
            Price = Convert.ToInt32(tokens[5]);
        }

        public int Price { get; set; }
    }

    class SubIOCInputCommand : SubLoInputCommand
    {

        public SubIOCInputCommand(string[] tokens) : base(tokens)
        { }

    }

    class SubFOKCInputCommand : SubLoInputCommand
    {

        public SubFOKCInputCommand(string[] tokens) : base(tokens)
        { }

    }


    class SubIcebergInputCommand : SubLoInputCommand
    {

        public SubIcebergInputCommand(string[] tokens) : base(tokens)
        {
            Quantity = Convert.ToInt32(tokens[6]);
            DisplaySize = Quantity;
            HiddenQuantity = Convert.ToInt32(tokens[4]) - Quantity;
        }

        private int DisplaySize { get; set; }

        public int HiddenQuantity { get; set; }

        public void Replenish()
        {
            if (Quantity < DisplaySize)
            {
                var diff = DisplaySize - Quantity;
                var take = diff > HiddenQuantity ? HiddenQuantity : diff;
                Quantity += take;
                HiddenQuantity -= take;
            }
        }
    }


    class CxlInputCommand : InputCommand
    {

        public CxlInputCommand(string[] tokens) : base(tokens)
        {
            Id = tokens[1];
        }

        public string Id { get; set; }
    }

    class CrpInputCommand : InputCommand
    {
        public CrpInputCommand(string[] tokens) : base(tokens)
        {
            Id = tokens[1];
            Quantity = Convert.ToInt32(tokens[2]);
            Price = Convert.ToInt32(tokens[3]);
        }

        public string Id { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
    }

    class EndInputCommand : InputCommand
    {
        public EndInputCommand(string[] tokens) : base(tokens)
        {
        }
    }


    class IcebergOrder : Order
    {
        public IcebergOrder(int price, int quantity, string id, int realQuantity)
            : base(price, quantity, id)
        {
            HiddenQuantity = realQuantity - quantity;
            DisplaySize = quantity;
        }


        public int HiddenQuantity { get; set; }
        public int DisplaySize { get; set; }

        public void Replenish()
        {
            if (Quantity < DisplaySize)
            {
                var diff = DisplaySize - Quantity;
                var take = diff > HiddenQuantity ? HiddenQuantity : diff;
                Quantity += take;
                HiddenQuantity -= take;
            }
        }

        public override string ToString()
        {
            return $"{Quantity}({HiddenQuantity+Quantity})@{Price}#{Id}";
        }
    }

    // I'd use record to not allow default ctor but not supported
    class Order
    {
        public Order(int price, int quantity, string id)
        {
            Id = id;
            Price = price;
            Quantity = quantity;
        }


        public string Id { get; private set; }
        public int Price { get; set; }
        public int Quantity { get; set; }

        //public virtual int GetRealQuantity()
        //{
        //    return Quantity;
        //}

        //public virtual void SetRealQuantity(int quantity)
        //{
        //    Quantity = quantity;
        //}

        public override string ToString()
        {
            return $"{Quantity}@{Price}#{Id}";
        }
    }
}