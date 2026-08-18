import { describe, expect, it } from 'vitest';
import { leaf, leaves, removeLeaf, splitLeaf } from '../src/layout';

describe('layout tree (ADR-0024)', () => {
  it('a leaf enumerates as itself', () => {
    expect(leaves(leaf('a'))).toEqual(['a']);
  });

  it('splitting a leaf yields both panes in order, new pane on the b-side', () => {
    const tree = splitLeaf(leaf('a'), 'a', 'row', 'b');
    expect(leaves(tree)).toEqual(['a', 'b']);
  });

  it('splitting a nested target splits only that leaf', () => {
    let tree = splitLeaf(leaf('a'), 'a', 'row', 'b');
    tree = splitLeaf(tree, 'b', 'col', 'c');
    expect(leaves(tree)).toEqual(['a', 'b', 'c']);
  });

  it('removing one side collapses to the sibling subtree', () => {
    const tree = splitLeaf(leaf('a'), 'a', 'row', 'b');
    const collapsed = removeLeaf(tree, 'a');
    expect(collapsed).not.toBeNull();
    expect(leaves(collapsed!)).toEqual(['b']);
  });

  it('removing the last leaf yields null (tab closes)', () => {
    expect(removeLeaf(leaf('a'), 'a')).toBeNull();
  });

  it('removing a nested leaf preserves the rest of the tree', () => {
    let tree = splitLeaf(leaf('a'), 'a', 'row', 'b');
    tree = splitLeaf(tree, 'b', 'col', 'c');
    const collapsed = removeLeaf(tree, 'b');
    expect(leaves(collapsed!)).toEqual(['a', 'c']);
  });

  it('removing an unknown id leaves the tree unchanged', () => {
    const tree = splitLeaf(leaf('a'), 'a', 'row', 'b');
    const result = removeLeaf(tree, 'nope');
    expect(leaves(result!)).toEqual(['a', 'b']);
  });
});
