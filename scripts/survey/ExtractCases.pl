# Tóm tắt từng case của cj_0.b: dòng, các lệnh đọc, hàm được gọi
# Có hai tầng switch: tầng 1 (case thụt 20 khoảng trắng) và tầng lồng từ dòng 4731 (thụt 24).
open F,"<../cfr-dec/nro/cj_0.java"; my $n=0; my ($cur,$line,@ops,@calls);
sub flush { return unless defined $cur; print "$cur\t$line\t".join(",",map{s/read//r}@ops)."\t".join(" ",@calls)."\n"; }
while(<F>){ $n++; next if $n<1999 || $n>5240;
 my $ind = $n>=4731 ? 24 : 20;
 if(/^\s{$ind}case (-?\d+):/){ flush(); $cur=$1;$line=$n;@ops=();@calls=(); next}
 if(/^\s{$ind}default:/){flush();undef $cur;next}
 next unless defined $cur;
 push @ops,$1 while /\.b\(\)\.(read\w+)\(/g;
 push @calls,"$1" while /((?:this|cj_0)\.\w+\(var1_1\))/g;
}
flush();
