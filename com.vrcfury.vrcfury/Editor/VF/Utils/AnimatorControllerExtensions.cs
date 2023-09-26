using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Inspector;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    public static class AnimatorControllerExtensions {
        public static void Rewrite(this AnimatorController c, AnimationRewriter rewriter) {
            // Rewrite clips
            foreach (var clip in new AnimatorIterator.Clips().From(c)) {
                clip.Rewrite(rewriter);
            }

            // Rewrite masks
            foreach (var layer in c.layers) {
                var mask = layer.avatarMask;
                if (mask == null || mask.transformCount == 0) continue;
                mask.SetTransforms(mask.GetTransforms()
                    .Select(rewriter.RewritePath)
                    .Where(path => path != null));
            }
        }

        public static void RewriteParameters(this AnimatorController c, Func<string, string> rewriteParamName, bool includeWrites = true) {
            IImmutableSet<StateMachineBehaviour> behaviors = null;

            // Collect data before calling the 'rewriteParamName' callback, which could add 
            // new data that shouldn't be rewritten agagin
            if (includeWrites) {
                behaviors = new AnimatorIterator.Behaviours().From(c);
            }
            var states = new AnimatorIterator.States().From(c);
            var clips = new AnimatorIterator.Clips().From(c);
            var trees = new AnimatorIterator.Trees().From(c);
            var transitions = new AnimatorIterator.Transitions().From(c);

            // Params
            if (includeWrites) {
                var prms = c.parameters;
                foreach (var p in prms) {
                    p.name = rewriteParamName(p.name);
                }

                c.parameters = prms;
            }

            // States
            foreach (var state in states) {
                state.speedParameter = rewriteParamName(state.speedParameter);
                state.cycleOffsetParameter = rewriteParamName(state.cycleOffsetParameter);
                state.mirrorParameter = rewriteParamName(state.mirrorParameter);
                state.timeParameter = rewriteParamName(state.timeParameter);
                VRCFuryEditorUtils.MarkDirty(state);
            }

            // Parameter Drivers
            if (includeWrites) {
                foreach (var b in behaviors) {
                    if (b is VRCAvatarParameterDriver oldB) {
                        foreach (var p in oldB.parameters) {
                            p.name = rewriteParamName(p.name);
                            var sourceField = p.GetType().GetField("source");
                            if (sourceField != null) {
                                sourceField.SetValue(p, rewriteParamName((string)sourceField.GetValue(p)));
                            }
                        }
                    }
                }
            }

            // Parameter Animations
            if (includeWrites) {
                foreach (var clip in clips) {
                    clip.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                        if (binding.path != "") return binding;
                        if (binding.type != typeof(Animator)) return binding;
                        if (binding.IsMuscle()) return binding;
                        binding.propertyName = rewriteParamName(binding.propertyName);
                        return binding;
                    }));
                }
            }

            // Blend trees
            foreach (var tree in trees) {
                tree.blendParameter = rewriteParamName(tree.blendParameter);
                tree.blendParameterY = rewriteParamName(tree.blendParameterY);
                tree.children = tree.children.Select(child => {
                    child.directBlendParameter = rewriteParamName(child.directBlendParameter);
                    return child;
                }).ToArray();
            }

            // Transitions
            foreach (var transition in transitions) {
                transition.conditions = transition.conditions.Select(cond => {
                    cond.parameter = rewriteParamName(cond.parameter);
                    return cond;
                }).ToArray();
                VRCFuryEditorUtils.MarkDirty(transition);
            }
            
            VRCFuryEditorUtils.MarkDirty(c);
        }
    }
}
