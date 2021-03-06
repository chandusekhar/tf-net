﻿using System;

namespace Topology.Graph
{
    public interface IMutableVertexAndEdgeListGraph<TVertex,TEdge> :
        IMutableVertexListGraph<TVertex,TEdge>,
        IMutableEdgeListGraph<TVertex,TEdge>,
        IVertexAndEdgeListGraph<TVertex,TEdge>
        where TEdge : IEdge<TVertex>
    {
        bool AddVerticesAndEdge(TEdge e);
    }
}
