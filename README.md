# UnityMPM

## Introduction

This is an Unity implementation of MPM(Material Point Method). There is no straight forward implantation of MPM in Unity so far. Other implementations usually skip/optimize steps of original mpm too much, which makes them difficult to understand at first time. They are also coded in c++/python. [nialltl's](https://github.com/nialltl/incremental_mpm) implementation is a very good MPM tutorial, but he only implements MLSMPM without snow. This repository starts from APIC framework, then try to follow original [mpm](https://dl.acm.org/doi/10.1145/2461912.2461948) and [mpm course](https://www.seas.upenn.edu/~cffjiang/mpmcourse.html) and match each step in the course. Then move to MLSMPM. It also contains 3D implementation on CPU/GPU. It is under development and needs optimization and parameter tuning.

## APIC

In APIC folder, there are CPU/GPU 2D/3D implementations of APIC. White squares are particles, red spheres are cell mass/velocity that transferred from particles. 

![](/gif/apic2d.gif)

![](/gif/apic3d.gif)

## MPM

In MPM folder, there are CPU/GPU 2D/3D implementations of MPM. It matches every step of original mpm course/paper. So it may be slow for real time.

MPMCPU implements "weakly" compressed fluid, snow and elastic material.

![](/gif/mpm2d.gif)

MPMGPU speeds up a bit.

![](/gif/mpmgpu.gif)

## MLSMPM

Implementation of A Moving Least Squares Material Point Method with Displacement Discontinuity and Two-Way Rigid Body Coupling

![](/gif/mlsmpm_elastic.gif)

![](/gif/mlsmpm_snow.gif)

![](/gif/mlsmpm_fluid.gif)

### There is also 3D version

![](/gif/mlsmpm_elastic3d.gif)